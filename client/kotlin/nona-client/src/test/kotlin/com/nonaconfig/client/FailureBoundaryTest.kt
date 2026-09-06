package com.nonaconfig.client

import java.io.File
import java.nio.file.Files
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import kotlin.test.*
import kotlin.time.Duration
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.collect
import org.json.JSONObject

class FailureBoundaryTest {
    private val options = NonaOptions("https://nona.test", "Production", minimumFetchInterval = Duration.ZERO)
    private fun client(store: NonaSnapshotStore) = NonaConfig.create(options, store, object : NonaHttpClient {
        override fun get(url: String, headers: Map<String, String>) = NonaHttpResponse(200, """{"flag":{"value":"A"}}""", null)
    })

    @Test fun `failed disk writes preserve memory and recover`() = runBlocking<Unit> {
        val root = Files.createTempDirectory("nona-disk-failure").toFile()
        try {
            val parent = File(root, "blocked").apply { writeText("not a directory") }
            val store = FileSnapshotStore(File(parent, "snapshot.json"))
            val config = client(store)
            config.fetchAndActivate()
            assertEquals("A", config.getString("flag"))
            assertNull(store.read())
            assertTrue(parent.delete())
            config.fetch()
            val restarted = client(store)
            assertTrue(restarted.initialize())
            assertEquals("A", restarted.getString("flag"))
        } finally { root.deleteRecursively() }
    }

    @Test fun `cancelled reset waiting for write does not clear committed values`() = runBlocking<Unit> {
        val entered = CountDownLatch(1)
        val release = CountDownLatch(1)
        val memory = InMemorySnapshotStore()
        val store = object : NonaSnapshotStore {
            override fun read() = memory.read()
            override fun write(json: String) { entered.countDown(); check(release.await(5, TimeUnit.SECONDS)); memory.write(json) }
            override fun clear() = memory.clear()
        }
        val config = client(store)
        val fetch = async(Dispatchers.IO) { config.fetch() }
        try {
            assertTrue(entered.await(5, TimeUnit.SECONDS))
            assertTrue(config.activate())
            val started = CompletableDeferred<Unit>()
            val reset = launch(Dispatchers.IO) { started.complete(Unit); config.reset() }
            started.await()
            reset.cancel()
            release.countDown()
            reset.join(); fetch.await()
            assertEquals("A", config.getString("flag"))
            assertNotNull(memory.read())
        } finally { release.countDown() }
    }

    @Test fun `concurrent file store instances never expose partial JSON`() = runBlocking<Unit> {
        val root = Files.createTempDirectory("nona-file-concurrency").toFile()
        try {
            val a = FileSnapshotStore(File(root, "snapshot.json"))
            val b = FileSnapshotStore(File(root, "snapshot.json"))
            val values = listOf("A", "B").map { JSONObject().put("flag", it.repeat(8192)).toString() }
            a.write(values[0])
            coroutineScope {
                (0 until 50).map { i -> launch(Dispatchers.IO) {
                    (if (i % 2 == 0) a else b).write(values[i % 2])
                    assertTrue(a.read() in values)
                } }.joinAll()
            }
        } finally { root.deleteRecursively() }
    }

    @Test fun `cache rejects wrong value types without losing defaults`() = runBlocking<Unit> {
        val store = InMemorySnapshotStore()
        client(store).fetch()
        val json = JSONObject(store.read()!!)
        json.getJSONObject("values").getJSONObject("flag").put("value", 123)
        store.write(json.toString())
        val restarted = client(store)
        restarted.setDefaults(mapOf("flag" to "default"))
        assertFalse(restarted.initialize())
        assertEquals("default", restarted.getString("flag"))
    }
    @Test fun `failed subscriber cannot stop another subscriber`() = runBlocking<Unit> {
        val updates = ConfigUpdates()
        val failed = CompletableDeferred<Unit>()
        val handler = CoroutineExceptionHandler { _, error ->
            assertIs<IllegalStateException>(error)
            failed.complete(Unit)
        }
        val scope = CoroutineScope(SupervisorJob() + Dispatchers.Unconfined + handler)
        val delivered = mutableListOf<Set<String>>()
        try {
            scope.launch { updates.flow.collect { throw IllegalStateException("consumer failure") } }
            scope.launch { updates.flow.collect { delivered += it } }
            updates.publish(setOf("first"))
            withTimeout(2000) { failed.await() }
            updates.publish(setOf("next"))
            assertEquals(listOf(setOf("first"), setOf("next")), delivered)
        } finally { scope.cancel() }
    }

}
