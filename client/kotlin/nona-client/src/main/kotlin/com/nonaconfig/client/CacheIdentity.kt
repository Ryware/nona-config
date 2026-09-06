package com.nonaconfig.client

import java.net.URI
import java.security.MessageDigest

internal fun NonaOptions.normalizedBaseUrl(): String {
    val uri = URI(baseUrl)
    val scheme = uri.scheme?.lowercase()
    require(scheme == "https" || scheme == "http") { "baseUrl must use HTTP or HTTPS" }
    require(uri.host != null && uri.rawUserInfo == null && uri.rawQuery == null && uri.rawFragment == null) {
        "baseUrl must have a host and no credentials, query or fragment"
    }
    val port = if ((scheme == "https" && uri.port == 443) || (scheme == "http" && uri.port == 80)) -1 else uri.port
    val origin = URI(scheme, null, uri.host.lowercase(), port, null, null, null).toASCIIString()
    return origin + uri.rawPath.orEmpty().trimEnd('/')
}

internal fun NonaOptions.cacheIdentity(): String {
    val digest = MessageDigest.getInstance("SHA-256")
    for (part in listOf("nona-cache-v2", normalizedBaseUrl(), apiKey, environmentId, prefix, releaseVersion)) {
        val bytes = part?.toByteArray(Charsets.UTF_8)
        digest.update("${bytes?.size ?: -1}:".toByteArray(Charsets.UTF_8))
        if (bytes != null) digest.update(bytes)
    }
    return digest.digest().joinToString("") { "%02x".format(it) }
}
