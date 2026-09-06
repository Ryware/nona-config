package com.nonaconfig.client

import kotlin.time.Duration
import kotlin.time.Duration.Companion.hours
import kotlin.time.Duration.Companion.seconds

/** Where a resolved value came from. */
enum class NonaValueSource { REMOTE, DEFAULT, STATIC }

/** Why a resolution failed. Maps one-to-one onto OpenFeature error codes. */
enum class NonaFailureReason { NOT_READY, NOT_FOUND, TYPE_MISMATCH, PARSE_ERROR }

/** What a call to [NonaConfig.fetch] did. `SUCCESS` still needs [NonaConfig.activate]. */
enum class FetchStatus { SUCCESS, NOT_MODIFIED, THROTTLED }

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
)

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
