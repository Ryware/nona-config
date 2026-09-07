package com.nonaconfig.client

import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import kotlin.test.*
import kotlin.time.Duration
import kotlin.time.Duration.Companion.hours
import kotlinx.coroutines.*
import org.junit.Test

class CacheAndLifecycleTest {
    private val options = NonaOptions("https://nona.test", "Production", "frontend")
    private val body = """{"flag":{"value":"true","contentType":"boolean"}}"""

    @Test fun `cache identity includes server project and unambiguous selectors`() {
        assertNotEquals(options.cacheIdentity(), options.copy(apiKey = "other").cacheIdentity())
        assertNotEquals(options.cacheIdentity(), options.copy(baseUrl = "https://other.test").cacheIdentity())
        assertNotEquals(options.copy(prefix = "Aa").cacheIdentity(), options.copy(prefix = "BB").cacheIdentity())
        assertNotEquals(options.cacheIdentity(), options.copy(useReleases = true).cacheIdentity())
        assertNotEquals(
            options.copy(useReleases = true).cacheIdentity(),
            options.copy(useReleases = true, releaseVersion = "1.0.0").cacheIdentity(),
        )
        assertEquals(options.cacheIdentity(), options.copy(baseUrl = "https://NONA.test:443/").cacheIdentity())
        assertFalse(options.cacheIdentity().contains("frontend"))
    }

    @Test fun `a shared custom store cannot restore another identity`() = runBlocking<Unit> {
        val store = InMemorySnapshotStore()
        val a = NonaConfig.create(options, store, NonaHttpClientFake { NonaHttpResponse(200, body, "a") })
        a.fetchAndActivate()
        val b = NonaConfig.create(options.copy(apiKey = "other"), store)
        assertFalse(b.initialize())
        assertEquals(NonaValueSource.STATIC, b.getSource("flag"))
    }

    @Test fun `304 preserves pending values and persists refreshed throttle timestamp`() = runBlocking<Unit> {
        val store = InMemorySnapshotStore()
        var now = 1000L
        var calls = 0
        val http = NonaHttpClientFake { headers ->
            calls++
            if (calls == 1) NonaHttpResponse(200, body, "a") else {
                assertEquals("a", headers["If-None-Match"])
                NonaHttpResponse(304, "", "a")
            }
        }
        val config = NonaConfig.create(options, store, http, { now })
        config.fetch()
        now += 13.hours.inWholeMilliseconds
        assertEquals(FetchStatus.NOT_MODIFIED, config.fetch())
        assertFalse(config.getBoolean("flag"))
        assertTrue(config.activate())
        val restored = NonaConfig.create(options, store, http, { now })
        assertTrue(restored.initialize())
        assertEquals(FetchStatus.THROTTLED, restored.fetch())
        assertEquals(2, calls)
    }

    @Test fun `304 without a snapshot fails without throttling the next attempt`() = runBlocking<Unit> {
        val config = NonaConfig.create(options, InMemorySnapshotStore(), NonaHttpClientFake { NonaHttpResponse(304, "", "a") })
        assertFailsWith<NonaException> { config.fetch() }
        assertFailsWith<NonaException> { config.fetch() }
    }

    @Test fun `concurrent activation consumes a pending snapshot only once`() = runBlocking<Unit> {
        val config = NonaConfig.create(options, InMemorySnapshotStore(), NonaHttpClientFake { NonaHttpResponse(200, body, "a") })
        config.fetch()
        val results = (1..20).map { async(Dispatchers.Default) { config.activate() } }.awaitAll()
        assertEquals(1, results.count { it })
    }

    @Test fun `cancelling a Java future prevents snapshot persistence`() = runBlocking<Unit> {
        val entered = CountDownLatch(1)
        val release = CountDownLatch(1)
        val store = InMemorySnapshotStore()
        var calls = 0
        lateinit var config: NonaConfig
        config = NonaConfig.create(options, store, NonaHttpClientFake {
            if (++calls > 1) {
                assertNull(store.read(), "Cancelled request must not persist")
                assertFalse(config.activate(), "Cancelled request must not leave pending values")
            }
            entered.countDown()
            check(release.await(5, TimeUnit.SECONDS))
            NonaHttpResponse(200, body, "a")
        })
        val future = config.fetchAsync()
        try {
            assertTrue(entered.await(5, TimeUnit.SECONDS))
            future.cancel(true)
        } finally { release.countDown() }
        // Acquiring fetchLock through another request waits for the cancelled request to exit.
        config.fetch(Duration.ZERO)
        assertEquals(FetchStatus.THROTTLED, config.fetch())
        assertTrue(config.activate())
    }

    @Test fun `clock rollback does not indefinitely throttle refresh`() = runBlocking<Unit> {
        var now = 1000L
        val config = NonaConfig.create(options, InMemorySnapshotStore(),
            NonaHttpClientFake { NonaHttpResponse(200, body, "a") }, { now })
        config.fetch()
        now = 500L
        assertEquals(FetchStatus.SUCCESS, config.fetch())
    }

    @Test fun `per fetch intervals must be finite and nonnegative`() = runBlocking<Unit> {
        val config = NonaConfig.create(options, InMemorySnapshotStore(),
            NonaHttpClientFake { error("Invalid intervals must not start a request") })
        assertFailsWith<IllegalArgumentException> { config.fetch(-1.hours) }
        assertFailsWith<IllegalArgumentException> { config.fetch(Duration.INFINITE) }
        assertFailsWith<IllegalArgumentException> { options.copy(maxResponseBytes = 0) }
    }

    @Test fun `normalization preserves escaped path separators`() {
        assertEquals("https://nona.test/proxy%2Ftenant", options.copy(baseUrl = "https://NONA.test:443/proxy%2Ftenant/").normalizedBaseUrl())
        assertNotEquals(options.copy(baseUrl = "https://nona.test/proxy%2Ftenant").cacheIdentity(),
            options.copy(baseUrl = "https://nona.test/proxy/tenant").cacheIdentity())
    }

    @Test fun `invalid timeouts are rejected before opening a connection`() {
        assertFailsWith<IllegalArgumentException> { options.copy(readTimeout = Duration.ZERO) }
        assertFailsWith<IllegalArgumentException> { options.copy(connectTimeout = Duration.INFINITE) }
        assertFailsWith<IllegalArgumentException> { options.copy(baseUrl = "file:///tmp/config") }
        assertFailsWith<IllegalArgumentException> { options.copy(minimumFetchInterval = -1.hours) }
    }
}

private class NonaHttpClientFake(private val reply: (Map<String, String>) -> NonaHttpResponse) : NonaHttpClient {
    override fun get(url: String, headers: Map<String, String>) = reply(headers)
}
