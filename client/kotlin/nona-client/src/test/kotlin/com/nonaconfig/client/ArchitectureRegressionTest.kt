package com.nonaconfig.client

import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import kotlin.test.*
import kotlin.time.Duration
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.collect
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.UnconfinedTestDispatcher

@OptIn(ExperimentalCoroutinesApi::class)
class ArchitectureRegressionTest {
    private val options = NonaOptions("https://nona.test", "production", "frontend")
    private fun body(vararg keys: String) = keys.joinToString(",", "{", "}") { "\"$it\":{\"value\":\"yes\",\"contentType\":\"text\"}" }
    private fun client(store: NonaSnapshotStore = InMemorySnapshotStore(), response: () -> String) =
        NonaConfig.create(options, store, object : NonaHttpClient {
            override fun get(url: String, headers: Map<String, String>) = NonaHttpResponse(200, response(), null)
        })

    @Test fun `slow subscriber keeps changed and deleted keys independently`() = runTest {
        var response = body("removed")
        val config = client { response }
        config.fetchAndActivate(Duration.ZERO)
        val slow = mutableListOf<Set<String>>()
        val fast = mutableListOf<Set<String>>()
        val release = CompletableDeferred<Unit>()
        val dispatcher = UnconfinedTestDispatcher(testScheduler)
        val a = backgroundScope.launch(dispatcher) {
            config.configUpdates.collect { slow += it; if (slow.size == 1) release.await() }
        }
        val b = backgroundScope.launch(dispatcher) { config.configUpdates.collect { fast += it } }
        response = body("removed", "first")
        config.fetchAndActivate(Duration.ZERO)
        response = body("removed", "first", "added")
        config.fetchAndActivate(Duration.ZERO)
        response = body("first", "added")
        config.fetchAndActivate(Duration.ZERO)
        release.complete(Unit)
        assertEquals(setOf("added", "removed"), slow.drop(1).flatten().toSet())
        assertEquals(listOf(setOf("first"), setOf("added"), setOf("removed")), fast)
        a.cancel(); b.cancel()
    }

    @Test fun `cancelled initialization does not commit a blocked cache read`() = runBlocking {
        val data = Snapshot(mapOf("flag" to NonaEntry("yes", "text")), null, 1000).toCacheJson(options.cacheIdentity())
        val entered = CountDownLatch(1)
        val release = CountDownLatch(1)
        val store = object : NonaSnapshotStore {
            override fun read(): String { entered.countDown(); check(release.await(5, TimeUnit.SECONDS)); return data }
            override fun write(json: String) = Unit
            override fun clear() = Unit
        }
        val config = client(store) { error("No HTTP expected") }
        val worker = launch(Dispatchers.IO) { config.initialize() }
        try { assertTrue(entered.await(5, TimeUnit.SECONDS)); worker.cancel() }
        finally { release.countDown() }
        worker.join()
        assertEquals(NonaValueSource.STATIC, config.getSource("flag"))
    }

    @Test fun `activation does not wait for disk writes and reset clears them in order`() = runBlocking {
        val entered = CountDownLatch(1)
        val release = CountDownLatch(1)
        val memory = InMemorySnapshotStore()
        val store = object : NonaSnapshotStore {
            override fun read() = memory.read()
            override fun write(json: String) { entered.countDown(); check(release.await(5, TimeUnit.SECONDS)); memory.write(json) }
            override fun clear() = memory.clear()
        }
        val config = client(store) { body("flag") }
        val fetch = async(Dispatchers.IO) { config.fetch() }
        try {
            assertTrue(entered.await(5, TimeUnit.SECONDS))
            val activated = CompletableDeferred<Boolean>()
            val activation = launch(Dispatchers.Default) { activated.complete(config.activate()) }
            try { assertTrue(withTimeout(1000) { activated.await() }) }
            finally { release.countDown(); activation.join() }
            config.reset()
            fetch.await()
            assertNull(memory.read())
            assertEquals(NonaValueSource.STATIC, config.getSource("flag"))
        } finally { release.countDown() }
    }

    @Test fun `invalid API header and dot selectors are rejected at construction`() {
        for (key in listOf("key\tvalue", "key\r\nvalue", "key\u007fvalue")) {
            assertFailsWith<IllegalArgumentException> { options.copy(apiKey = key) }
        }
        for (id in listOf(".", "..")) assertFailsWith<IllegalArgumentException> { options.copy(environmentId = id) }
    }

    @Test fun `options diagnostics redact keys and invalid ports are rejected`() {
        assertFalse(options.toString().contains("frontend"))
        for (port in listOf(0, 65536)) {
            assertFailsWith<IllegalArgumentException> { options.copy(baseUrl = "https://nona.test:$port") }
        }
    }

    @Test fun `concurrent publishers preserve all keys and new subscribers get no history`() = runTest {
        val updates = ConfigUpdates()
        val received = mutableSetOf<String>()
        val unblock = CompletableDeferred<Unit>()
        val dispatcher = UnconfinedTestDispatcher(testScheduler)
        val collector = backgroundScope.launch(dispatcher) {
            updates.flow.collect { received.addAll(it); if ("initial" in it) unblock.await() }
        }
        updates.publish(setOf("initial"))
        coroutineScope { (1..100).map { launch(Dispatchers.Default) { updates.publish(setOf("key-$it")) } }.joinAll() }
        unblock.complete(Unit)
        assertEquals((1..100).map { "key-$it" }.toSet() + "initial", received)
        collector.cancelAndJoin()
        updates.publish(setOf("no-subscriber"))
        val fresh = mutableListOf<Set<String>>()
        val next = backgroundScope.launch(dispatcher) { updates.flow.collect { fresh += it } }
        assertTrue(fresh.isEmpty())
        updates.publish(setOf("new"))
        assertEquals(listOf(setOf("new")), fresh)
        next.cancelAndJoin()
    }

    @Test fun `custom transport cannot bypass response byte limit`() = runBlocking {
        val config = NonaConfig.create(options.copy(maxResponseBytes = 8), InMemorySnapshotStore(), object : NonaHttpClient {
            override fun get(url: String, headers: Map<String, String>) = NonaHttpResponse(200, body("flag"), null)
        })
        assertFailsWith<NonaException> { config.fetch() }
        assertFalse(config.activate())
    }
}
