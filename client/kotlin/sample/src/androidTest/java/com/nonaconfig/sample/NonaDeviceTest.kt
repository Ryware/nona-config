package com.nonaconfig.sample

import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import com.nonaconfig.client.*
import java.net.HttpURLConnection
import java.net.URL
import java.util.concurrent.TimeUnit
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.collect
import kotlin.time.Duration
import org.junit.Assert.*
import org.junit.Test
import org.junit.runner.RunWith
import android.content.Intent
import android.view.View
import android.view.ViewGroup
import android.widget.Button
import android.widget.TextView

@Retention(AnnotationRetention.RUNTIME)
@Target(AnnotationTarget.FUNCTION)
annotation class ManualProbe

/** Uses only disposable fixtures created by client/qa/seed-server.py. */
@RunWith(AndroidJUnit4::class)
class NonaDeviceTest {
    private val context = InstrumentationRegistry.getInstrumentation().targetContext
    private val args = InstrumentationRegistry.getArguments()
    private val baseUrl = args.getString("baseUrl", "http://10.0.2.2:18686")
    private fun key(name: String = "frontendA") = requireNotNull(args.getString(name)) { "Missing QA argument $name" }
    private fun options(apiKey: String = key()) = NonaOptions.builder(baseUrl, "Production")
        .apiKey(apiKey).minimumFetchIntervalMillis(0).build()
    private fun activeRelease(version: String) {
        val connection = URL("$baseUrl/admin/projects/sdk-qa-a/environments/Production/active-release/").openConnection() as HttpURLConnection
        try {
            connection.requestMethod = "PUT"
            connection.connectTimeout = 5000
            connection.readTimeout = 5000
            connection.setRequestProperty("Authorization", "Bearer ${key("adminToken")}")
            connection.setRequestProperty("Content-Type", "application/json")
            connection.doOutput = true
            connection.outputStream.use { it.write("{\"version\":\"$version\"}".toByteArray()) }
            check(connection.responseCode in 200..299) { "Could not select fixture release" }
        } finally { connection.disconnect() }
    }

    @Test fun slowSubscriberRetainsChangedAndDeletedKeys() = runBlocking {
        var body = """{"removed":{"value":"yes","contentType":"text"}}"""
        val config = NonaConfig.create(options(), InMemorySnapshotStore(), object : NonaHttpClient {
            override fun get(url: String, headers: Map<String, String>) = NonaHttpResponse(200, body, null)
        })
        config.fetchAndActivate()
        val first = CompletableDeferred<Unit>()
        val release = CompletableDeferred<Unit>()
        val delivered = CompletableDeferred<Set<String>>()
        var seenFirst = false
        val collector = launch(start = CoroutineStart.UNDISPATCHED) {
            config.configUpdates.collect {
                if (!seenFirst) {
                    seenFirst = true
                    first.complete(Unit)
                    release.await()
                } else delivered.complete(it)
            }
        }
        try {
            body = """{"removed":{"value":"yes"},"first":{"value":"yes"}}"""
            config.fetchAndActivate()
            withTimeout(2000) { first.await() }
            body = """{"removed":{"value":"yes"},"first":{"value":"yes"},"added":{"value":"yes"}}"""
            config.fetchAndActivate()
            body = """{"first":{"value":"yes"},"added":{"value":"yes"}}"""
            config.fetchAndActivate()
            release.complete(Unit)
            assertEquals(setOf("added", "removed"), withTimeout(2000) { delivered.await() })
        } finally { collector.cancelAndJoin() }
    }

    @Test fun interruptedBodiesCannotReplaceActiveCache() = runBlocking {
        val base = args.getString("faultBaseUrl", "http://10.0.2.2:18687")
        for (route in listOf("short-body", "partial-json", "disconnect", "trickle")) {
            val opts = NonaOptions.builder("$base/$route", "Production").minimumFetchIntervalMillis(0)
                .connectTimeoutMillis(5000).readTimeoutMillis(3000).maxResponseBytes(64).build()
            val store = InMemorySnapshotStore()
            val seed = NonaConfig.create(opts, store, object : NonaHttpClient {
                override fun get(url: String, headers: Map<String, String>) =
                    NonaHttpResponse(200, """{"flag":{"value":"old"}}""", null)
            })
            seed.fetch()
            val original = store.read()
            val config = NonaConfig.create(opts, store)
            assertTrue(config.initialize())
            try { config.fetch(); fail("Expected $route failure") } catch (_: NonaException) { }
            assertEquals(route, "old", config.getString("flag"))
            assertFalse(route, config.activate())
            assertEquals(route, original, store.read())
        }
    }

    @Test fun sharedContractMatchesAndroidPlatformJSON() = runBlocking {
        val corpus = org.json.JSONObject(InstrumentationRegistry.getInstrumentation().context.assets
            .open("snapshots.json").bufferedReader().use { it.readText() })
        val cases = corpus.getJSONArray("snapshots")
        for (i in 0 until cases.length()) {
            val item = cases.getJSONObject(i)
            val name = item.getString("name")
            val config = NonaConfig.create(options(), InMemorySnapshotStore(), object : NonaHttpClient {
                override fun get(url: String, headers: Map<String, String>) = NonaHttpResponse(200, item.getString("body"), null)
            })
            val accepted = try { config.fetchAndActivate(); true } catch (_: NonaException) { false }
            assertEquals(name, item.getBoolean("valid"), accepted)
            if (accepted) {
                val expected = item.getJSONObject("expected")
                assertEquals(name, expected.keys().asSequence().toSet(), config.keys)
                for (key in config.keys) {
                    val entry = expected.getJSONObject(key)
                    val resolved = config.resolveString(key) as NonaResolution.Success
                    assertEquals(name, entry.getString("value"), resolved.value)
                    assertEquals(name, entry.getString("contentType"), resolved.contentType)
                }
            }
        }
        val values = corpus.getJSONArray("values")
        for (i in 0 until values.length()) {
            val item = values.getJSONObject(i)
            val raw = item.getString("value")
            val config = NonaConfig.create(options(), InMemorySnapshotStore(), object : NonaHttpClient {
                override fun get(url: String, headers: Map<String, String>) = NonaHttpResponse(200,
                    org.json.JSONObject().put("flag", org.json.JSONObject().put("value", raw)).toString(), null)
            })
            config.fetchAndActivate()
            assertEquals(raw, if (item.isNull("long")) 0L else item.getString("long").toLong(), config.getLong("flag"))
            assertEquals(raw, if (item.isNull("double")) 0.0 else item.getDouble("double"), config.getDouble("flag"), 0.0)
            assertEquals(raw, item.getBoolean("boolean"), config.getBoolean("flag"))
        }
    }

    @Test fun corruptedCacheCannotCoerceNumberIntoString() = runBlocking {
        val store = InMemorySnapshotStore()
        val opts = options()
        NonaConfig.create(opts, store, object : NonaHttpClient {
            override fun get(url: String, headers: Map<String, String>) = NonaHttpResponse(200, """{"flag":{"value":"old"}}""", null)
        }).fetch()
        val json = org.json.JSONObject(store.read()!!)
        json.getJSONObject("values").getJSONObject("flag").put("value", 123)
        store.write(json.toString())
        val config = NonaConfig.create(opts, store)
        config.setDefaults(mapOf("flag" to "default"))
        assertFalse(config.initialize())
        assertEquals("default", config.getString("flag"))
    }

    @Test @ManualProbe fun processKillProbe() = runBlocking<Unit> {
        org.junit.Assume.assumeTrue(args.getString("crashProbe") == "true")
        val directory = java.io.File(context.filesDir, "process-kill-probe")
        directory.deleteRecursively()
        check(directory.mkdirs())
        val scoped = object : android.content.ContextWrapper(context) {
            override fun getFilesDir() = directory
        }
        val config = NonaConfig.create(scoped, options())
        config.fetch()
        java.io.File(directory, "ready").writeText("ready")
        while (true) config.fetch(Duration.ZERO)
    }

    @Test fun javaApiFetchActivateAnd304() {
        activeRelease("1.0.0")
        val config = JavaClient.create(context, baseUrl, key())
        config.resetAsync().get(5, TimeUnit.SECONDS)
        config.setDefaults(mapOf("flag" to "default", "Limits:Retries" to 3))
        assertEquals(FetchStatus.SUCCESS, config.fetchAsync().get(5, TimeUnit.SECONDS))
        assertEquals("default", config.getString("flag"))
        assertTrue(config.activate())
        assertEquals("A", config.getString("flag"))
        assertEquals(3L, config.getLong("Limits:Retries"))
        assertEquals(FetchStatus.NOT_MODIFIED, config.fetchAsync().get(5, TimeUnit.SECONDS))
        assertFalse(config.activate())
        assertFalse(config.keys.contains("Hidden"))
    }

    @Test fun defaultDiskCacheIsolatesProjectsAndRestoresWithoutNetwork() = runBlocking {
        activeRelease("1.0.0")
        val a = NonaConfig.create(context, options())
        val b = NonaConfig.create(context, options(key("frontendB")))
        a.reset(); b.reset()
        a.fetchAndActivate()
        assertFalse(b.initialize())
        assertEquals("", b.getString("flag"))
        b.fetchAndActivate()
        assertEquals("B", b.getString("flag"))
        val restoredA = NonaConfig.create(context, options())
        assertTrue(restoredA.initialize())
        assertEquals("A", restoredA.getString("flag"))
        a.reset(); b.reset()
    }

    @Test fun rollbackCannotActivateAnObsoletePendingRelease() = runBlocking {
        activeRelease("1.0.0")
        val config = NonaConfig.create(context, options())
        config.reset()
        try {
            config.fetchAndActivate()
            activeRelease("2.0.0")
            config.fetch()
            assertEquals("A", config.getString("flag"))
            activeRelease("1.0.0")
            assertFalse(config.fetchAndActivate())
            assertEquals("A", config.getString("flag"))
        } finally { activeRelease("1.0.0"); config.reset() }
    }

    @Test fun serverRejectsBackendAndUnknownKeys() = runBlocking {
        for ((apiKey, expected) in listOf(key("backendKey") to 404, "A".repeat(64) to 401)) {
            val config = NonaConfig.create(options(apiKey), InMemorySnapshotStore())
            try {
                config.fetch()
                fail("Expected HTTP $expected")
            } catch (error: NonaHttpException) { assertEquals(expected, error.statusCode) }
        }
    }

    @Test fun prefixPinnedReleaseAndThrottleUseRealServer() = runBlocking {
        val pinned = options().copy(prefix = "Features:", releaseVersion = "1.0.0")
        val config = NonaConfig.create(pinned, InMemorySnapshotStore())
        config.fetchAndActivate()
        assertEquals(setOf("Features:Checkout"), config.keys)
        assertTrue(config.getBoolean("Features:Checkout"))
        assertEquals(FetchStatus.THROTTLED, config.fetch(kotlin.time.Duration.parse("1h")))
    }

    @Test fun transportTimeoutAndMalformedSnapshotPreserveDefaults() = runBlocking {
        val faultBase = args.getString("faultBaseUrl", "http://10.0.2.2:18687")
        for (route in listOf("slow", "malformed", "http503")) {
            val opts = NonaOptions.builder("$faultBase/$route", "Production")
                .connectTimeoutMillis(5000).readTimeoutMillis(if (route == "slow") 100 else 5000).build()
            val config = NonaConfig.create(opts, InMemorySnapshotStore())
            config.setDefaults(mapOf("flag" to "fallback"))
            try {
                config.fetch(Duration.ZERO)
                fail("Expected $route failure")
            } catch (error: NonaException) {
                when (route) {
                    "slow" -> assertTrue("Expected timeout, got ${error.cause?.javaClass?.simpleName}", error.cause is java.net.SocketTimeoutException)
                    "malformed" -> assertTrue("Expected invalid value, got ${error.message}", error.message.orEmpty().contains("non-string"))
                    "http503" -> assertEquals(503, (error as NonaHttpException).statusCode)
                }
                assertEquals("fallback", config.getString("flag"))
            }
        }
    }

    @Test fun oversizedResponsesAreRejectedEvenWithoutContentLength() = runBlocking {
        val faultBase = args.getString("faultBaseUrl", "http://10.0.2.2:18687")
        for (route in listOf("large", "large-no-length")) {
            val config = NonaConfig.create(
                NonaOptions.builder("$faultBase/$route", "Production").maxResponseBytes(64).build(),
                InMemorySnapshotStore(),
            )
            config.setDefaults(mapOf("flag" to "fallback"))
            try {
                config.fetch()
                fail("Expected response size limit")
            } catch (error: NonaException) {
                assertTrue(error.message.orEmpty().contains("maxResponseBytes"))
                assertFalse(config.activate())
                assertEquals("fallback", config.getString("flag"))
            }
        }
    }

    @Test fun redirectsCannotChangeTheTrustedServer() = runBlocking {
        val faultBase = args.getString("faultBaseUrl", "http://10.0.2.2:18687")
        val config = NonaConfig.create(
            NonaOptions.builder("$faultBase/redirect", "Production").apiKey("test-only-key").build(),
            InMemorySnapshotStore(),
        )
        try {
            config.fetch()
            fail("The HTTP client must reject redirects")
        } catch (error: NonaHttpException) {
            assertEquals(302, error.statusCode)
            assertFalse(config.activate())
        }
    }

    @Test fun malformedLaunchUrlDoesNotPoisonSavedConnection() {
        val preferences = context.getSharedPreferences("connection", android.content.Context.MODE_PRIVATE)
        preferences.edit().putString("url", baseUrl).putString("key", key()).commit()
        val instrumentation = InstrumentationRegistry.getInstrumentation()
        val activity = instrumentation.startActivitySync(Intent(context, MainActivity::class.java)
            .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
            .putExtra("frontendKey", "test-only-key").putExtra("baseUrl", "not a URL")) as MainActivity
        try {
            assertEquals(baseUrl, preferences.getString("url", null))
            assertEquals(key(), preferences.getString("key", null))
            instrumentation.runOnMainSync {
                val content = activity.findViewById<ViewGroup>(android.R.id.content).getChildAt(0) as ViewGroup
                assertTrue((0 until content.childCount).map { content.getChildAt(it) }
                    .filterIsInstance<TextView>().any { it.text == activity.getString(R.string.invalid_connection) })
            }
        } finally { instrumentation.runOnMainSync { activity.finish() } }
    }

    @Test fun sampleButtonsHandleResetFetchAndActivate() {
        activeRelease("1.0.0")
        val instrumentation = InstrumentationRegistry.getInstrumentation()
        val activity = instrumentation.startActivitySync(Intent(context, MainActivity::class.java)
            .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
            .putExtra("frontendKey", key()).putExtra("baseUrl", baseUrl)) as MainActivity
        fun views(view: View): List<View> = listOf(view) + if (view is ViewGroup)
            (0 until view.childCount).flatMap { views(view.getChildAt(it)) } else emptyList()
        fun awaitText(expected: String) {
            val deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(5)
            while (System.nanoTime() < deadline) {
                var found = false
                instrumentation.runOnMainSync {
                    found = views(activity.window.decorView).filterIsInstance<TextView>()
                        .any { it.text.contains(expected) }
                }
                if (found) return
                Thread.sleep(20)
            }
            fail("Sample UI did not display $expected")
        }
        fun click(label: Int) = instrumentation.runOnMainSync {
            views(activity.window.decorView).filterIsInstance<Button>()
                .single { it.text.toString() == activity.getString(label) }.performClick()
        }
        try {
            awaitText("Cache restored:")
            click(R.string.reset)
            awaitText("Reset: done")
            awaitText("flag: default")
            click(R.string.fetch)
            awaitText("Fetch: SUCCESS")
            awaitText("flag: default")
            click(R.string.activate)
            awaitText("flag: A")
            awaitText("source: REMOTE")
        } finally { instrumentation.runOnMainSync { activity.finish() } }
    }
}
