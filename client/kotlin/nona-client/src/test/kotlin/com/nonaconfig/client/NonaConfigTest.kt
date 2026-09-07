package com.nonaconfig.client

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertFalse
import kotlin.test.assertNull
import kotlin.test.assertTrue
import kotlin.time.Duration
import kotlin.time.Duration.Companion.hours
import kotlin.time.Duration.Companion.minutes
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.runTest

private const val SNAPSHOT_BODY = """
{
  "Features:Checkout": { "value": "true", "contentType": "boolean" },
  "Limits:Retries": { "value": "42", "contentType": "number" },
  "Copy:Title": { "value": "Checkout", "contentType": "text" },
  "Theme": { "value": "{\"color\":\"green\"}", "contentType": "json" }
}
"""

private class FakeHttpClient(
    private var handler: (Map<String, String>) -> NonaHttpResponse,
) : NonaHttpClient {
    val requests = mutableListOf<Pair<String, Map<String, String>>>()

    override fun get(url: String, headers: Map<String, String>): NonaHttpResponse {
        requests += url to headers
        return handler(headers)
    }

    fun respondWith(handler: (Map<String, String>) -> NonaHttpResponse) {
        this.handler = handler
    }
}

private fun ok(body: String = SNAPSHOT_BODY, etag: String? = "W/\"1\"") =
    NonaHttpResponse(200, body, etag)

private class TestClock(var nowMillis: Long = 1_000L) : () -> Long {
    override fun invoke(): Long = nowMillis
}

@OptIn(kotlinx.coroutines.ExperimentalCoroutinesApi::class)
private fun config(
    http: NonaHttpClient,
    store: NonaSnapshotStore = InMemorySnapshotStore(),
    clock: () -> Long = TestClock(),
    options: NonaOptions = NonaOptions(
        baseUrl = "https://nona.test",
        environmentId = "production",
        apiKey = "frontend-key",
    ),
) = NonaConfig.create(
    options = options,
    store = store,
    http = http,
    clock = clock,
    ioDispatcher = UnconfinedTestDispatcher(),
)

@OptIn(kotlinx.coroutines.ExperimentalCoroutinesApi::class)
class NonaConfigTest {

    @Test
    fun `reads typed values from an activated snapshot`() = runTest {
        val http = FakeHttpClient { ok() }
        val config = config(http)

        assertEquals(FetchStatus.SUCCESS, config.fetch())
        assertTrue(config.activate())

        assertTrue(config.getBoolean("Features:Checkout"))
        assertEquals(42L, config.getLong("Limits:Retries"))
        assertEquals(42.0, config.getDouble("Limits:Retries"))
        assertEquals("Checkout", config.getString("Copy:Title"))
        assertEquals("""{"color":"green"}""", config.getString("Theme"))

        assertEquals(1, http.requests.size, "reads must not touch the network")
        assertEquals("https://nona.test/api/production/parameters", http.requests[0].first)
        assertEquals("frontend-key", http.requests[0].second["X-Api-Key"])
    }

    @Test
    fun `fetch does not change reads until activate`() = runTest {
        val http = FakeHttpClient { ok() }
        val config = config(http)
        config.setDefaults(mapOf("Features:Checkout" to false))

        config.fetch()
        assertFalse(config.getBoolean("Features:Checkout"), "fetch alone must not apply values")
        assertEquals(NonaValueSource.DEFAULT, config.getSource("Features:Checkout"))

        config.activate()
        assertTrue(config.getBoolean("Features:Checkout"))
        assertEquals(NonaValueSource.REMOTE, config.getSource("Features:Checkout"))
    }

    @Test
    fun `defaults are used until a value arrives, then static zero after that`() = runTest {
        val http = FakeHttpClient { ok() }
        val config = config(http)
        config.setDefaults(mapOf("Copy:Title" to "Fallback", "Missing:Flag" to true))

        assertEquals("Fallback", config.getString("Copy:Title"))
        assertTrue(config.getBoolean("Missing:Flag"))
        assertEquals(NonaValueSource.STATIC, config.getSource("Nothing:Here"))
        assertFalse(config.getBoolean("Nothing:Here"))

        config.fetchAndActivate()

        assertEquals("Checkout", config.getString("Copy:Title"))
        assertTrue(config.getBoolean("Missing:Flag"), "defaults still cover keys the server omits")
        assertEquals(NonaValueSource.DEFAULT, config.getSource("Missing:Flag"))
    }

    @Test
    fun `activate reports changed keys`() = runTest {
        val http = FakeHttpClient { ok() }
        val config = config(http)
        val updates = mutableListOf<Set<String>>()

        config.fetchAndActivate()
        backgroundScope.launch(UnconfinedTestDispatcher(testScheduler)) {
            config.configUpdates.collect { updates += it }
        }

        http.respondWith {
            ok(
                body = """
                {
                  "Features:Checkout": { "value": "false", "contentType": "boolean" },
                  "Limits:Retries": { "value": "42", "contentType": "number" },
                  "Copy:Title": { "value": "Checkout", "contentType": "text" },
                  "Added": { "value": "new", "contentType": "text" }
                }
                """.trimIndent(),
                etag = "W/\"2\"",
            )
        }

        assertTrue(config.fetchAndActivate(Duration.ZERO))
        assertEquals(1, updates.size)
        assertEquals(setOf("Features:Checkout", "Theme", "Added"), updates.single())
    }

    @Test
    fun `activate returns false when nothing moved`() = runTest {
        val http = FakeHttpClient { ok() }
        val config = config(http)

        assertTrue(config.fetchAndActivate())
        assertFalse(config.fetchAndActivate(Duration.ZERO), "identical values are not a change")
    }

    @Test
    fun `a 304 keeps the active values and sends the stored etag`() = runTest {
        val http = FakeHttpClient { ok() }
        val config = config(http)
        config.fetchAndActivate()

        http.respondWith { headers ->
            assertEquals("W/\"1\"", headers["If-None-Match"])
            NonaHttpResponse(304, "", "W/\"1\"")
        }

        assertEquals(FetchStatus.NOT_MODIFIED, config.fetch(Duration.ZERO))
        assertFalse(config.activate())
        assertTrue(config.getBoolean("Features:Checkout"))
    }

    @Test
    fun `a failed release refresh preserves the last known good snapshot and etag`() = runTest {
        val http = FakeHttpClient { ok() }
        val config = config(
            http,
            options = NonaOptions(
                baseUrl = "https://nona.test",
                environmentId = "production",
                apiKey = "frontend-key",
                useReleases = true,
                minimumFetchInterval = Duration.ZERO,
            ),
        )
        config.fetchAndActivate()

        http.respondWith {
            NonaHttpResponse(
                409,
                """{"title":"Conflict","status":409,"detail":"No active release","errorCode":"active_release_not_configured"}""",
                null,
            )
        }
        val failure = assertFailsWith<NonaHttpException> { config.fetch() }
        assertEquals("active_release_not_configured", failure.errorCode)
        assertTrue(config.getBoolean("Features:Checkout"))

        http.respondWith { headers ->
            assertEquals("W/\"1\"", headers["If-None-Match"])
            NonaHttpResponse(304, "", "W/\"1\"")
        }
        assertEquals(FetchStatus.NOT_MODIFIED, config.fetch())
        assertTrue(config.getBoolean("Features:Checkout"))
    }

    @Test
    fun `fetches inside the minimum interval are throttled`() = runTest {
        val clock = TestClock()
        val http = FakeHttpClient { ok() }
        val config = config(http, clock = clock)

        config.fetchAndActivate()
        assertEquals(1, http.requests.size)

        clock.nowMillis += 5.minutes.inWholeMilliseconds
        assertEquals(FetchStatus.THROTTLED, config.fetch())
        assertEquals(1, http.requests.size, "throttled fetches must not hit the network")

        clock.nowMillis += 12.hours.inWholeMilliseconds
        assertEquals(FetchStatus.SUCCESS, config.fetch())
        assertEquals(2, http.requests.size)
    }

    @Test
    fun `a cached snapshot survives a restart`() = runTest {
        val store = InMemorySnapshotStore()
        config(FakeHttpClient { ok() }, store = store).fetchAndActivate()

        val restarted = config(
            FakeHttpClient { error("the network must not be needed to read the cache") },
            store = store,
        )
        assertTrue(restarted.initialize())
        assertTrue(restarted.getBoolean("Features:Checkout"))
        assertEquals(NonaValueSource.REMOTE, restarted.getSource("Features:Checkout"))
    }

    @Test
    fun `initialize reports false with no cache and leaves reads not ready`() = runTest {
        val config = config(FakeHttpClient { ok() })

        assertFalse(config.initialize())
        val resolution = config.resolveBoolean("Features:Checkout")
        assertEquals(
            NonaFailureReason.NOT_READY,
            (resolution as NonaResolution.Failure).reason,
        )
    }

    @Test
    fun `a corrupt cache is ignored rather than fatal`() = runTest {
        val store = InMemorySnapshotStore()
        store.write("{ this is not json")

        val config = config(FakeHttpClient { ok() }, store = store)
        assertFalse(config.initialize())
    }

    @Test
    fun `resolution reports why a read failed`() = runTest {
        val http = FakeHttpClient {
            ok(
                body = """{ "NotABoolean": { "value": "yes", "contentType": "text" } }""",
                etag = null,
            )
        }
        val config = config(http)
        config.fetchAndActivate()

        val mistyped = config.resolveBoolean("NotABoolean") as NonaResolution.Failure
        assertEquals(NonaFailureReason.TYPE_MISMATCH, mistyped.reason)
        assertFalse(config.getBoolean("NotABoolean"), "the getter still falls back")

        val missing = config.resolveString("Nope") as NonaResolution.Failure
        assertEquals(NonaFailureReason.NOT_FOUND, missing.reason)
        assertTrue(missing.message.contains("frontend-scoped"))

        val ok = config.resolveString("NotABoolean") as NonaResolution.Success
        assertEquals("yes", ok.value)
        assertEquals("text", ok.contentType)
        assertEquals(NonaValueSource.REMOTE, ok.source)
    }

    @Test
    fun `http failures carry their status code`() = runTest {
        val config = config(FakeHttpClient {
            NonaHttpResponse(
                404,
                """{"title":"Not Found","status":404,"detail":"Environment missing","errorCode":"environment_not_found"}""",
                null,
            )
        })

        val thrown = assertFailsWith<NonaHttpException> { config.fetch() }
        assertEquals(404, thrown.statusCode)
        assertEquals("environment_not_found", thrown.errorCode)
        assertEquals("Environment missing", thrown.detail)
    }

    @Test
    fun `a rejected key is reported as such`() = runTest {
        val config = config(FakeHttpClient { NonaHttpResponse(401, "", null) })

        val thrown = assertFailsWith<NonaHttpException> { config.fetch() }
        assertEquals(401, thrown.statusCode)
    }

    @Test
    fun `release source prefix and selector become part of the route`() = runTest {
        val http = FakeHttpClient { ok() }
        val config = config(
            http,
            options = NonaOptions(
                baseUrl = "https://nona.test/",
                environmentId = "pre production",
                useReleases = true,
                releaseVersion = "1.4.x",
                prefix = "Features:",
            ),
        )

        config.fetch()

        assertEquals(
            "https://nona.test/api/pre%20production/releases/parameters?version=1.4.x&prefix=Features%3A",
            http.requests.single().first,
        )
        assertNull(http.requests.single().second["X-Api-Key"], "no key configured, no header")
    }

    @Test
    fun `release source without selector uses the active release route`() = runTest {
        val http = FakeHttpClient { ok() }
        val config = config(
            http,
            options = NonaOptions(
                baseUrl = "https://nona.test",
                environmentId = "production",
                useReleases = true,
            ),
        )

        config.fetch()

        assertEquals("https://nona.test/api/production/releases/parameters", http.requests.single().first)
    }

    @Test
    fun `release selector is rejected when release mode is disabled`() {
        assertFailsWith<IllegalArgumentException> {
            NonaOptions(
                baseUrl = "https://nona.test",
                environmentId = "production",
                releaseVersion = "1.4.x",
            )
        }
    }

    @Test
    fun `keys covers the snapshot and the defaults`() = runTest {
        val config = config(FakeHttpClient { ok() })
        config.setDefaults(mapOf("Only:Default" to 1))
        config.fetchAndActivate()

        // Order is not part of the contract: org.json parses into a HashMap.
        assertEquals(
            setOf("Features:Checkout", "Limits:Retries", "Copy:Title", "Theme", "Only:Default"),
            config.keys,
        )
    }

    @Test
    fun `reset clears memory and disk`() = runTest {
        val store = InMemorySnapshotStore()
        val config = config(FakeHttpClient { ok() }, store = store)
        config.fetchAndActivate()

        config.reset()

        assertNull(store.read())
        assertFalse(config.getBoolean("Features:Checkout"))
    }
}
