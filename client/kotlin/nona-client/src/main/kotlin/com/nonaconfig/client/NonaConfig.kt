package com.nonaconfig.client

import android.content.Context
import java.io.File
import java.io.IOException
import java.net.URLEncoder
import java.util.concurrent.CompletableFuture
import kotlin.time.Duration
import kotlinx.coroutines.CoroutineDispatcher
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext
import kotlinx.coroutines.future.future
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive
import org.json.JSONObject

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
    private val stateLock = Any()
    // Cache operations serialize before state mutation; never perform I/O under stateLock.
    private val cacheLock = Mutex()
    private var generation = 0L
    private val cacheIdentity = options.cacheIdentity()

    @Volatile
    private var active: Snapshot? = null

    @Volatile
    private var pending: Snapshot? = null

    @Volatile
    private var defaults: Map<String, NonaEntry> = emptyMap()

    @Volatile
    private var lastFetchAtMillis: Long = 0

    private val updates = ConfigUpdates()

    /** Per-collector activation invalidations. Slow collectors receive a union of changed keys. */
    val configUpdates: Flow<Set<String>> = updates.flow

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
        cacheLock.withLock {
            if (synchronized(stateLock) { active != null || pending != null }) return@withLock false
            val restored = store.read()?.let { Snapshot.fromCacheJson(it, cacheIdentity) }
                ?: return@withLock false
            val context = currentCoroutineContext()
            synchronized(stateLock) {
                context.ensureActive()
                active = restored
                lastFetchAtMillis = restored.fetchedAtMillis
            }
            true
        }
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
            require(minimumFetchInterval.isFinite() && minimumFetchInterval >= Duration.ZERO) {
                "minimumFetchInterval must be finite and nonnegative"
            }
            val now = clock()
            if (lastFetchAtMillis > 0 && now >= lastFetchAtMillis &&
                now - lastFetchAtMillis < minimumFetchInterval.inWholeMilliseconds
            ) {
                return@withLock FetchStatus.THROTTLED
            }

            withContext(ioDispatcher) { load(now) }
        }

    /** Promotes fetched values into the active set. Returns true if they changed. */
    fun activate(): Boolean {
        val changed = synchronized(stateLock) {
            val next = pending ?: return false
            pending = null
            changedKeys(active?.values.orEmpty(), next.values).also { active = next }
        }
        // Collectors can run inline; never invoke them while holding stateLock.
        if (changed.isNotEmpty()) updates.publish(changed)
        return changed.isNotEmpty()
    }

    /** [fetch] followed by [activate]. Returns true if the active values changed. */
    suspend fun fetchAndActivate(
        minimumFetchInterval: Duration = options.minimumFetchInterval,
    ): Boolean {
        if (fetch(minimumFetchInterval) == FetchStatus.DISCARDED) return false
        return activate()
    }

    /** Forgets the cached snapshot on disk and in memory. */
    suspend fun reset() = withContext(ioDispatcher) {
        cacheLock.withLock {
            val context = currentCoroutineContext()
            synchronized(stateLock) {
                context.ensureActive()
                generation++
                active = null
                pending = null
                lastFetchAtMillis = 0
            }
            store.clear()
        }
    }

    /** Java-friendly asynchronous counterparts; cancel the future to cancel the coroutine. */
    fun initializeAsync(): CompletableFuture<Boolean> = async { initialize() }
    fun fetchAsync(): CompletableFuture<FetchStatus> = async { fetch() }
    fun fetchAndActivateAsync(): CompletableFuture<Boolean> = async { fetchAndActivate() }
    fun resetAsync(): CompletableFuture<Void?> = async { reset(); null }

    fun getBoolean(key: String): Boolean = valueOrDefault(key, ValueParsing::parseBoolean, false)

    fun getString(key: String): String = valueOrDefault(key, ValueParsing::parseString, "")

    fun getLong(key: String): Long = valueOrDefault(key, ValueParsing::parseLong, 0L)

    fun getDouble(key: String): Double = valueOrDefault(key, ValueParsing::parseDouble, 0.0)

    private fun <T> async(block: suspend () -> T): CompletableFuture<T> =
        CoroutineScope(ioDispatcher).future { block() }

    private fun <T> valueOrDefault(
        key: String,
        parse: (String, String) -> ValueParsing.ParseResult<T>,
        zero: T,
    ): T {
        val remote = active?.values?.get(key)?.let { parse(key, it.value) }
        if (remote is ValueParsing.ParseResult.Ok) return remote.value
        val fallback = defaults[key]?.let { parse(key, it.value) }
        return if (fallback is ValueParsing.ParseResult.Ok) fallback.value else zero
    }

    /** Raw entry origin; a typed getter may fall back if that entry cannot be parsed. */
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

    private suspend fun load(now: Long): FetchStatus {
        val (requestGeneration, previous) = synchronized(stateLock) { generation to (pending ?: active) }
        val headers = buildMap {
            options.apiKey?.let { put("X-Api-Key", it) }
            previous?.etag?.let { put("If-None-Match", it) }
        }

        val response = try {
            http.get(snapshotUrl(), headers)
        } catch (cause: IOException) {
            throw NonaException("Nona request failed.", cause)
        }
        currentCoroutineContext().ensureActive()
        when (response.statusCode) {
            in 200..299 -> Unit

            HTTP_NOT_MODIFIED -> {
                if (previous == null) throw NonaException("Nona returned 304 without a cached snapshot.")
                return persist(previous.copy(fetchedAtMillis = now), requestGeneration, revalidated = true)
            }

            else -> {
                val problem = parseProblemDetails(response.body)
                throw NonaHttpException(
                    statusCode = response.statusCode,
                    message = problem.detail ?: describeHttpFailure(response.statusCode),
                    errorCode = problem.errorCode,
                    detail = problem.detail,
                )
            }
        }

        if (response.body.toByteArray(Charsets.UTF_8).size > options.maxResponseBytes) {
            throw NonaException("Nona snapshot exceeds maxResponseBytes (${options.maxResponseBytes}).")
        }
        val snapshot = Snapshot.fromResponseBody(response.body, response.etag, now)
        return persist(snapshot, requestGeneration, revalidated = false)
    }

    private suspend fun persist(snapshot: Snapshot, requestGeneration: Long, revalidated: Boolean): FetchStatus =
        cacheLock.withLock {
            val json = snapshot.toCacheJson(cacheIdentity)
            val context = currentCoroutineContext()
            synchronized(stateLock) {
                context.ensureActive()
                if (generation != requestGeneration) return@withLock FetchStatus.DISCARDED
                if (revalidated && pending == null) active = snapshot else pending = snapshot
                lastFetchAtMillis = snapshot.fetchedAtMillis
            }
            store.write(json)
            if (revalidated) FetchStatus.NOT_MODIFIED else FetchStatus.SUCCESS
        }

    private fun describeHttpFailure(statusCode: Int): String = when (statusCode) {
        401 -> "Nona rejected the API key."
        404 -> "Nona has no snapshot for environment '${options.environmentId}'. " +
            "Check the environment id, and that the API key is frontend-scoped: " +
            "the snapshot endpoint is hidden from backend-only keys."

        else -> "Nona returned HTTP $statusCode."
    }

    private fun snapshotUrl(): String {
        val base = options.normalizedBaseUrl()
        val releaseVersion = options.releaseVersion?.trim()?.takeIf { options.useReleases && it.isNotEmpty() }
        val query = buildList {
            options.prefix?.let { add("prefix=" + encode(it)) }
        }.joinToString("&")

        val path = if (options.useReleases) {
            val release = releaseVersion?.let(::encode) ?: "active"
            "$base/api/environments/${encode(options.environmentId)}/releases/$release/parameters"
        } else {
            "$base/api/environments/${encode(options.environmentId)}/parameters"
        }
        return if (query.isEmpty()) path else "$path?$query"
    }

    private fun parseProblemDetails(body: String): ProblemDetails {
        if (body.isBlank()) return ProblemDetails()
        return try {
            val json = JSONObject(body)
            ProblemDetails(
                errorCode = json.opt("errorCode")?.takeIf { it is String } as? String,
                detail = json.opt("detail")?.takeIf { it is String } as? String,
            )
        } catch (_: Exception) {
            ProblemDetails()
        }
    }

    companion object {
        private const val HTTP_NOT_MODIFIED = 304

        /** Creates an instance that caches its snapshot in the app's private files. */
        @JvmStatic
        fun create(context: Context, options: NonaOptions): NonaConfig =
            create(
                options = options,
                store = FileSnapshotStore(
                    File(context.filesDir, "nona/${cacheFileName(options)}"),
                ),
            )

        /** Creates an instance with a supplied store and, optionally, HTTP client. */
        @JvmStatic
        @JvmOverloads
        fun create(
            options: NonaOptions,
            store: NonaSnapshotStore,
            http: NonaHttpClient = UrlConnectionHttpClient(
                connectTimeoutMillis = options.connectTimeout.inWholeMilliseconds.toInt(),
                readTimeoutMillis = options.readTimeout.inWholeMilliseconds.toInt(),
                maxResponseBytes = options.maxResponseBytes,
            ),
            clock: () -> Long = System::currentTimeMillis,
            ioDispatcher: CoroutineDispatcher = Dispatchers.IO,
        ): NonaConfig = NonaConfig(options, http, store, clock, ioDispatcher)

        /** Bound to origin, key/project and snapshot selectors, without exposing the key. */
        private fun cacheFileName(options: NonaOptions): String {
            return "snapshot-${options.cacheIdentity()}.json"
        }
    }
}

private data class ProblemDetails(
    val errorCode: String? = null,
    val detail: String? = null,
)

private fun encode(value: String): String =
    URLEncoder.encode(value, "UTF-8").replace("+", "%20")

private fun Any.toEntry(): NonaEntry = when (this) {
    is Boolean -> NonaEntry(toString(), "boolean")
    is Number -> NonaEntry(toString(), "number")
    is String -> NonaEntry(this, "text")
    else -> NonaEntry(toString(), "text")
}
