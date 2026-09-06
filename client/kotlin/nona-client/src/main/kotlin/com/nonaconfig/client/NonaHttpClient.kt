package com.nonaconfig.client

import java.io.ByteArrayOutputStream
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
 * app's existing OkHttp stack, or to fake the network in tests. Custom transports
 * must enforce redirect, timeout and streaming size policies themselves.
 */
interface NonaHttpClient {
    /** Performs a GET. Should throw [java.io.IOException] only for transport failures. */
    fun get(url: String, headers: Map<String, String>): NonaHttpResponse
}

internal class UrlConnectionHttpClient(
    private val connectTimeoutMillis: Int,
    private val readTimeoutMillis: Int,
    private val maxResponseBytes: Int,
) : NonaHttpClient {

    override fun get(url: String, headers: Map<String, String>): NonaHttpResponse {
        val connection = URL(url).openConnection() as HttpURLConnection
        return try {
            connection.requestMethod = "GET"
            // Credentials and configuration must stay on the configured origin.
            connection.instanceFollowRedirects = false
            // Nona owns revalidation; do not reuse a process-wide HTTP cache.
            connection.useCaches = false
            connection.connectTimeout = connectTimeoutMillis
            connection.readTimeout = readTimeoutMillis
            connection.setRequestProperty("Accept", "application/json")
            headers.forEach(connection::setRequestProperty)

            val status = connection.responseCode
            // Error bodies are unused. Do not download them before reporting the status.
            val body = if (status in 200..299) {
                connection.inputStream.use { stream ->
                    val output = ByteArrayOutputStream()
                    val buffer = ByteArray(DEFAULT_BUFFER_SIZE)
                    while (true) {
                        val count = stream.read(buffer)
                        if (count == -1) break
                        if (count > maxResponseBytes - output.size()) {
                            throw NonaException("Nona snapshot exceeds maxResponseBytes ($maxResponseBytes).")
                        }
                        output.write(buffer, 0, count)
                    }
                    output.toString("UTF-8")
                }
            } else ""

            NonaHttpResponse(
                statusCode = status,
                body = body,
                etag = connection.getHeaderField("ETag"),
            )
        } catch (cause: IOException) {
            throw NonaException("Nona request failed.", cause)
        } finally {
            connection.disconnect()
        }
    }
}
