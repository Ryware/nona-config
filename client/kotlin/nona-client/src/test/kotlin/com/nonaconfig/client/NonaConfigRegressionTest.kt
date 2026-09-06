package com.nonaconfig.client

import java.io.File
import java.nio.file.Files
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import kotlin.test.*
import kotlin.time.Duration
import kotlinx.coroutines.*

class NonaConfigRegressionTest {
    private fun options(key: String = "project-a") = NonaOptions("https://nona.test", "production", apiKey = key)
    private fun body(value: String) = """{"flag":{"value":"$value","contentType":"text"}}"""
    private fun client(http: NonaHttpClient, store: NonaSnapshotStore = InMemorySnapshotStore()) =
        NonaConfig.create(options(), store, http, clock = { 1000L }, ioDispatcher = Dispatchers.IO)

    @Test fun `malformed remote value must retain configured default`() = runBlocking {
        val config = client(object : NonaHttpClient {
            override fun get(url: String, headers: Map<String, String>) = NonaHttpResponse(200, body("not-a-number"), "a")
        })
        config.setDefaults(mapOf("flag" to 3))
        config.fetchAndActivate()
        assertEquals(3L, config.getLong("flag"))
    }

    @Test fun `304 after server rollback must not activate obsolete pending snapshot`() = runBlocking {
        var current = "A"
        val config = client(object : NonaHttpClient {
            override fun get(url: String, headers: Map<String, String>): NonaHttpResponse =
                if (headers["If-None-Match"] == current) NonaHttpResponse(304, "", current)
                else NonaHttpResponse(200, body(current), current)
        })
        config.fetchAndActivate(Duration.ZERO)
        current = "B"
        config.fetch(Duration.ZERO)
        current = "A"
        config.fetchAndActivate(Duration.ZERO)
        assertEquals("A", config.getString("flag"))
    }

    @Test fun `default cache must isolate project keys`() = runBlocking {
        val root = Files.createTempDirectory("nona-cache-review").toFile()
        try {
            fun store(key: String) = FileSnapshotStore(File(root, "snapshot-${options(key).cacheIdentity()}.json"))
            val first = NonaConfig.create(options("project-a"), store("project-a"), object : NonaHttpClient {
                override fun get(url: String, headers: Map<String, String>) = NonaHttpResponse(200, body("project-a-only"), "a")
            })
            first.fetchAndActivate()
            val second = NonaConfig.create(options("project-b"), store("project-b"), object : NonaHttpClient {
                override fun get(url: String, headers: Map<String, String>): NonaHttpResponse = error("No network expected")
            })
            val restored = second.initialize()
            println("project-b restored=$restored value=${second.getString("flag")}")
            assertFalse(restored, "Project B must not load project A's snapshot")
        } finally { root.deleteRecursively() }
    }

    @Test fun `reset must not be undone by an in-flight fetch`() = runBlocking {
        val entered = CountDownLatch(1)
        val release = CountDownLatch(1)
        val store = InMemorySnapshotStore()
        val config = client(object : NonaHttpClient {
            override fun get(url: String, headers: Map<String, String>): NonaHttpResponse {
                entered.countDown()
                check(release.await(5, TimeUnit.SECONDS))
                return NonaHttpResponse(200, body("old-session"), "a")
            }
        }, store)
        val fetch = async(Dispatchers.IO) { config.fetch() }
        try {
            assertTrue(entered.await(5, TimeUnit.SECONDS))
            config.reset()
        } finally { release.countDown() }
        fetch.await()
        assertNull(store.read(), "Reset must leave disk cache empty")
        assertFalse(config.activate(), "Reset must discard the in-flight snapshot")
    }
}
