package com.nonaconfig.client

import kotlin.time.Duration
import kotlin.time.Duration.Companion.hours
import kotlin.time.Duration.Companion.seconds
import kotlin.time.Duration.Companion.milliseconds

/** Where a resolved value came from. */
enum class NonaValueSource { REMOTE, DEFAULT, STATIC }

/** Why a resolution failed. Maps one-to-one onto OpenFeature error codes. */
enum class NonaFailureReason { NOT_READY, NOT_FOUND, TYPE_MISMATCH, PARSE_ERROR }

/** What a call to [NonaConfig.fetch] did. `SUCCESS` still needs [NonaConfig.activate]. */
enum class FetchStatus { SUCCESS, NOT_MODIFIED, THROTTLED, DISCARDED }

/**
 * The outcome of a typed read. The plain getters collapse this into a value;
 * the `resolve*` functions return it so callers can see why a fallback was used.
 */
sealed interface NonaResolution<out T> {
    data class Success<out T>(
        val value: T,
        val source: NonaValueSource,
        val contentType: String?,
    ) : NonaResolution<T>

    data class Failure(
        val reason: NonaFailureReason,
        val message: String,
    ) : NonaResolution<Nothing>
}

/** One raw entry in a Nona snapshot. */
data class NonaEntry(
    val value: String,
    val contentType: String,
)

data class NonaOptions(
    val baseUrl: String,
    val environmentId: String,
    /**
     * A **frontend-scoped** key. A backend-only key cannot read the snapshot
     * endpoint, and any key shipped inside an APK should be assumed public.
     */
    val apiKey: String? = null,
    /** Pin to an exact release (`1.4.0`) or a release line (`1.4.x`). */
    val releaseVersion: String? = null,
    /** Only load keys under this prefix, for example `Features:`. */
    val prefix: String? = null,
    /** Fetches closer together than this are throttled without touching the network. */
    val minimumFetchInterval: Duration = 12.hours,
    val connectTimeout: Duration = 10.seconds,
    val readTimeout: Duration = 10.seconds,
    /** Maximum decoded response bytes read by the default HTTP transport. */
    val maxResponseBytes: Int = 8 * 1024 * 1024,
) {
    init {
        normalizedBaseUrl()
        require(maxResponseBytes > 0) { "maxResponseBytes must be positive" }
        require(environmentId.isNotBlank() && environmentId != "." && environmentId != "..") {
            "environmentId must be nonblank and cannot be a dot path segment"
        }
        require(apiKey == null || apiKey.none { it.isISOControl() }) {
            "apiKey cannot contain control characters"
        }
        require(minimumFetchInterval.isFinite() && minimumFetchInterval >= Duration.ZERO) {
            "minimumFetchInterval must be finite and nonnegative"
        }
        for (timeout in listOf(connectTimeout, readTimeout)) {
            require(timeout.isFinite() && timeout.inWholeMilliseconds in 1..Int.MAX_VALUE.toLong()) {
                "Timeouts must be between 1 ms and Int.MAX_VALUE ms"
            }
        }
    }

    /** Safe diagnostic representation: never include the application's key. */
    override fun toString(): String =
        "NonaOptions(baseUrl=$baseUrl, environmentId=$environmentId, apiKey=<redacted>, " +
            "releaseVersion=$releaseVersion, prefix=$prefix, minimumFetchInterval=$minimumFetchInterval, " +
            "connectTimeout=$connectTimeout, readTimeout=$readTimeout, maxResponseBytes=$maxResponseBytes)"

    /** Java entry point without Kotlin inline-duration parameters. */
    class Builder internal constructor(private val baseUrl: String, private val environmentId: String) {
        private var apiKey: String? = null
        private var releaseVersion: String? = null
        private var prefix: String? = null
        private var minimumFetchInterval: Duration = 12.hours
        private var connectTimeout: Duration = 10.seconds
        private var readTimeout: Duration = 10.seconds
        private var maxResponseBytes: Int = 8 * 1024 * 1024

        fun apiKey(value: String?) = apply { apiKey = value }
        fun releaseVersion(value: String?) = apply { releaseVersion = value }
        fun prefix(value: String?) = apply { prefix = value }
        fun minimumFetchIntervalMillis(value: Long) = apply { minimumFetchInterval = value.milliseconds }
        fun connectTimeoutMillis(value: Long) = apply { connectTimeout = value.milliseconds }
        fun readTimeoutMillis(value: Long) = apply { readTimeout = value.milliseconds }
        fun maxResponseBytes(value: Int) = apply { maxResponseBytes = value }
        fun build() = NonaOptions(
            baseUrl, environmentId, apiKey, releaseVersion, prefix,
            minimumFetchInterval, connectTimeout, readTimeout, maxResponseBytes,
        )
    }

    companion object {
        @JvmStatic
        fun builder(baseUrl: String, environmentId: String) = Builder(baseUrl, environmentId)
    }
}


open class NonaException(
    message: String,
    cause: Throwable? = null,
) : Exception(message, cause)

/**
 * A 404 is ambiguous on purpose: the snapshot endpoint reports an unknown
 * environment and a backend-only key identically, so that a server key cannot
 * enumerate environments.
 */
class NonaHttpException(
    val statusCode: Int,
    message: String,
) : NonaException(message)
