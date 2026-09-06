package com.nonaconfig.client

import java.io.IOException
import java.net.HttpURLConnection
import java.net.URL

/** One HTTP response, reduced to what the snapshot loader needs. */
data class NonaHttpResponse(
    val statusCode: Int,
    val body: String,
    val etag: String?,
)

/**
 * Seam for the network call.
 *
 * The default implementation uses [HttpURLConnection], which is backed by
 * OkHttp on Android and needs no extra dependency. Supply your own to reuse an
 * app's existing OkHttp stack, or to fake the network in tests.
 */
interface NonaHttpClient {
    /** Performs a GET. Should throw [java.io.IOException] only for transport failures. */
    fun get(url: String, headers: Map<String, String>): NonaHttpResponse
}

internal class UrlConnectionHttpClient(
    private val connectTimeoutMillis: Int,
    private val readTimeoutMillis: Int,
) : NonaHttpClient {

    override fun get(url: String, headers: Map<String, String>): NonaHttpResponse {
        val connection = URL(url).openConnection() as HttpURLConnection
        return try {
            connection.requestMethod = "GET"
            connection.connectTimeout = connectTimeoutMillis
            connection.readTimeout = readTimeoutMillis
            connection.setRequestProperty("Accept", "application/json")
            headers.forEach(connection::setRequestProperty)

            val status = connection.responseCode
            // A 304 has no body, and errorStream carries the body for 4xx/5xx.
            val stream = when {
                status == HTTP_NOT_MODIFIED -> null
                status in 200..299 -> connection.inputStream
                else -> connection.errorStream
            }
            val body = stream?.bufferedReader()?.use { it.readText() }.orEmpty()

            NonaHttpResponse(
                statusCode = status,
                body = body,
                etag = connection.getHeaderField("ETag"),
            )
        } catch (cause: IOException) {
            throw NonaException("Nona request failed: ${cause.message}", cause)
        } finally {
            connection.disconnect()
        }
    }

    private companion object {
        const val HTTP_NOT_MODIFIED = 304
    }
}
