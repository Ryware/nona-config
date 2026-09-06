package com.nonaconfig.client

import android.content.Context
import java.io.File
import java.net.URLEncoder
import kotlin.time.Duration
import kotlinx.coroutines.CoroutineDispatcher
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.channels.BufferOverflow
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext

/**
 * Reads Nona config on Android. Values are held in memory and read
 * synchronously, downloading is separate from applying, and the last good
 * snapshot survives restarts.
 *
 * Every value is the same for every user; Nona has no runtime targeting.
 */
class NonaConfig internal constructor(
    private val options: NonaOptions,
    private val http: NonaHttpClient,
    private val store: NonaSnapshotStore,
    private val clock: () -> Long,
    private val ioDispatcher: CoroutineDispatcher,
) {

    private val fetchLock = Mutex()

    @Volatile
    private var active: Snapshot? = null

    @Volatile
    private var pending: Snapshot? = null

    @Volatile
    private var defaults: Map<String, NonaEntry> = emptyMap()

    @Volatile
    private var lastFetchAtMillis: Long = 0

    private val _configUpdates = MutableSharedFlow<Set<String>>(
        extraBufferCapacity = 1,
        onBufferOverflow = BufferOverflow.DROP_OLDEST,
    )

    /** Emits the changed keys every time [activate] applies new values. */
    val configUpdates: SharedFlow<Set<String>> = _configUpdates.asSharedFlow()

    /** Keys readable right now, from the active snapshot and the defaults. */
    val keys: Set<String>
        get() = LinkedHashSet<String>().apply {
            active?.values?.keys?.let(::addAll)
            addAll(defaults.keys)
        }

    /** Values used until a key arrives from Nona. Accepts booleans, numbers and strings. */
    fun setDefaults(values: Map<String, Any>) {
        defaults = values.entries.associate { (key, value) -> key to value.toEntry() }
    }

    /**
     * Loads the persisted snapshot into the active set; no network work. Call
     * once at startup, before the first read. Returns true if one was restored.
     */
    suspend fun initialize(): Boolean = withContext(ioDispatcher) {
        val restored = store.read()?.let(Snapshot::fromCacheJson) ?: return@withContext false
        active = restored
        lastFetchAtMillis = restored.fetchedAtMillis
        true
    }

    /**
     * Downloads into the pending slot; reads keep the activated values until
     * [activate].
     *
     * @throws NonaHttpException if Nona rejects the request.
     * @throws NonaException on a transport or parsing failure.
     */
    suspend fun fetch(minimumFetchInterval: Duration = options.minimumFetchInterval): FetchStatus =
        fetchLock.withLock {
            val now = clock()
            if (lastFetchAtMillis > 0 &&
                now - lastFetchAtMillis < minimumFetchInterval.inWholeMilliseconds
            ) {
                return@withLock FetchStatus.THROTTLED
            }

            withContext(ioDispatcher) { load(now) }
        }

    /** Promotes fetched values into the active set. Returns true if they changed. */
    fun activate(): Boolean {
        val next = pending ?: return false
        pending = null

        val changed = changedKeys(active?.values.orEmpty(), next.values)
        active = next
        if (changed.isEmpty()) {
            return false
        }

        _configUpdates.tryEmit(changed)
        return true
    }

    /** [fetch] followed by [activate]. Returns true if the active values changed. */
    suspend fun fetchAndActivate(
        minimumFetchInterval: Duration = options.minimumFetchInterval,
    ): Boolean {
        fetch(minimumFetchInterval)
        return activate()
    }

    /** Forgets the cached snapshot on disk and in memory. */
    suspend fun reset() = withContext(ioDispatcher) {
        active = null
        pending = null
        lastFetchAtMillis = 0
        store.clear()
    }

    fun getBoolean(key: String): Boolean = resolveBoolean(key).valueOr(false)

    fun getString(key: String): String = resolveString(key).valueOr("")

    fun getLong(key: String): Long = resolveLong(key).valueOr(0L)

    fun getDouble(key: String): Double = resolveDouble(key).valueOr(0.0)

    /** Where [key] would resolve from right now. */
    fun getSource(key: String): NonaValueSource =
        entryFor(key)?.second ?: NonaValueSource.STATIC

    fun resolveBoolean(key: String): NonaResolution<Boolean> =
        resolve(key, ValueParsing::parseBoolean)

    /**
     * Resolves the raw stored string. JSON values come back unparsed, so an
     * OpenFeature provider can decode them into whatever shape it needs.
     */
    fun resolveString(key: String): NonaResolution<String> =
        resolve(key, ValueParsing::parseString)

    fun resolveLong(key: String): NonaResolution<Long> =
        resolve(key, ValueParsing::parseLong)

    fun resolveDouble(key: String): NonaResolution<Double> =
        resolve(key, ValueParsing::parseDouble)

    private fun <T> resolve(
        key: String,
        parse: (String, String) -> ValueParsing.ParseResult<T>,
    ): NonaResolution<T> {
        val (entry, source) = entryFor(key) ?: return missing(key)

        return when (val parsed = parse(key, entry.value)) {
            is ValueParsing.ParseResult.Ok -> NonaResolution.Success(
                value = parsed.value,
                source = source,
                contentType = entry.contentType,
            )

            is ValueParsing.ParseResult.Err ->
                NonaResolution.Failure(parsed.reason, parsed.message)
        }
    }

    /** The single place that knows a remote value outranks a default. */
    private fun entryFor(key: String): Pair<NonaEntry, NonaValueSource>? =
        active?.values?.get(key)?.let { it to NonaValueSource.REMOTE }
            ?: defaults[key]?.let { it to NonaValueSource.DEFAULT }

    private fun missing(key: String): NonaResolution.Failure = if (active == null) {
        NonaResolution.Failure(
            NonaFailureReason.NOT_READY,
            "No Nona snapshot has been loaded yet, and '$key' has no default.",
        )
    } else {
        NonaResolution.Failure(
            NonaFailureReason.NOT_FOUND,
            "Nona key '$key' is not in the '${options.environmentId}' snapshot. " +
                "Client-side reads only see frontend-scoped entries.",
        )
    }

    private fun load(now: Long): FetchStatus {
        val headers = buildMap {
            options.apiKey?.let { put("X-Api-Key", it) }
            active?.etag?.let { put("If-None-Match", it) }
        }

        val response = http.get(snapshotUrl(), headers)
        when (response.statusCode) {
            in 200..299 -> Unit

            HTTP_NOT_MODIFIED -> {
                lastFetchAtMillis = now
                return FetchStatus.NOT_MODIFIED
            }

            else -> throw NonaHttpException(
                statusCode = response.statusCode,
                message = describeHttpFailure(response.statusCode),
            )
        }

        val snapshot = Snapshot.fromResponseBody(response.body, response.etag, now)
        pending = snapshot
        lastFetchAtMillis = now
        store.write(snapshot.toCacheJson())
        return FetchStatus.SUCCESS
    }

    private fun describeHttpFailure(statusCode: Int): String = when (statusCode) {
        401 -> "Nona rejected the API key."
        404 -> "Nona has no snapshot for environment '${options.environmentId}'. " +
            "Check the environment id, and that the API key is frontend-scoped: " +
            "the snapshot endpoint is hidden from backend-only keys."

        else -> "Nona returned HTTP $statusCode."
    }

    private fun snapshotUrl(): String {
        val base = options.baseUrl.trimEnd('/')
        val query = buildList {
            options.releaseVersion?.let { add("version=" + encode(it)) }
            options.prefix?.let { add("prefix=" + encode(it)) }
        }.joinToString("&")

        val path = "$base/api/${encode(options.environmentId)}"
        return if (query.isEmpty()) path else "$path?$query"
    }

    companion object {
        private const val HTTP_NOT_MODIFIED = 304

        /** Creates an instance that caches its snapshot in the app's private files. */
        fun create(context: Context, options: NonaOptions): NonaConfig =
            create(
                options = options,
                store = FileSnapshotStore(
                    File(context.filesDir, "nona/${cacheFileName(options)}"),
                ),
            )

        /** Creates an instance with a supplied store and, optionally, HTTP client. */
        fun create(
            options: NonaOptions,
            store: NonaSnapshotStore,
            http: NonaHttpClient = UrlConnectionHttpClient(
                connectTimeoutMillis = options.connectTimeout.inWholeMilliseconds.toInt(),
                readTimeoutMillis = options.readTimeout.inWholeMilliseconds.toInt(),
            ),
            clock: () -> Long = System::currentTimeMillis,
            ioDispatcher: CoroutineDispatcher = Dispatchers.IO,
        ): NonaConfig = NonaConfig(options, http, store, clock, ioDispatcher)

        /** Distinct per environment, prefix and pinned release, so caches cannot collide. */
        private fun cacheFileName(options: NonaOptions): String {
            val parts = listOf(
                options.environmentId,
                options.prefix.orEmpty(),
                options.releaseVersion.orEmpty(),
            )
            return "snapshot-${parts.joinToString("|").hashCode().toUInt().toString(16)}.json"
        }
    }
}

private fun encode(value: String): String =
    URLEncoder.encode(value, "UTF-8").replace("+", "%20")

private fun Any.toEntry(): NonaEntry = when (this) {
    is Boolean -> NonaEntry(toString(), "boolean")
    is Number -> NonaEntry(toString(), "number")
    is String -> NonaEntry(this, "text")
    else -> NonaEntry(toString(), "text")
}

private fun <T> NonaResolution<T>.valueOr(fallback: T): T = when (this) {
    is NonaResolution.Success -> value
    is NonaResolution.Failure -> fallback
}
