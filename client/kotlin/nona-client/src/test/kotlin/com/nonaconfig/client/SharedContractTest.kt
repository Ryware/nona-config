package com.nonaconfig.client

import kotlin.test.*
import kotlin.time.Duration
import kotlinx.coroutines.runBlocking
import org.json.JSONObject

class SharedContractTest {
    @Test fun `snapshot corpus has identical acceptance and values`() = runBlocking {
        val cases = JSONObject(SharedContracts.json).getJSONArray("snapshots")
        for (index in 0 until cases.length()) {
            val item = cases.getJSONObject(index)
            val name = item.getString("name")
            val options = NonaOptions("https://nona.test", "Production", minimumFetchInterval = Duration.ZERO)
            val config = NonaConfig.create(options, InMemorySnapshotStore(), object : NonaHttpClient {
                override fun get(url: String, headers: Map<String, String>) = NonaHttpResponse(200, item.getString("body"), "etag")
            })
            if (!item.getBoolean("valid")) {
                assertFailsWith<NonaException>(name) { config.fetch() }
                assertFalse(config.activate(), name)
                continue
            }
            config.fetchAndActivate()
            val expected = item.getJSONObject("expected")
            assertEquals(expected.keys().asSequence().toSet(), config.keys, name)
            for (key in config.keys) {
                val entry = expected.getJSONObject(key)
                val resolved = assertIs<NonaResolution.Success<String>>(config.resolveString(key), name)
                assertEquals(entry.getString("value"), resolved.value, name)
                assertEquals(entry.getString("contentType"), resolved.contentType, name)
            }
        }
    }

    @Test fun `numeric and boolean corpus has identical fallback semantics`() = runBlocking {
        val cases = JSONObject(SharedContracts.json).getJSONArray("values")
        for (index in 0 until cases.length()) {
            val item = cases.getJSONObject(index)
            val value = item.getString("value")
            val config = NonaConfig.create(NonaOptions("https://nona.test", "Production"), InMemorySnapshotStore(), object : NonaHttpClient {
                override fun get(url: String, headers: Map<String, String>) = NonaHttpResponse(200,
                    JSONObject().put("flag", JSONObject().put("value", value)).toString(), null)
            })
            config.fetchAndActivate()
            assertEquals(if (item.isNull("long")) 0L else item.getString("long").toLong(), config.getLong("flag"), value)
            assertEquals(if (item.isNull("double")) 0.0 else item.getDouble("double"), config.getDouble("flag"), value)
            assertEquals(item.getBoolean("boolean"), config.getBoolean("flag"), value)
        }
    }
    @Test fun `cache written by the old cache identity is ignored`() = runBlocking {
        val fixture = JSONObject(SharedContracts.json).getJSONObject("cacheFixtures").getJSONObject("kotlin")
        val store = InMemorySnapshotStore().apply { write(fixture.getString("json")) }
        val config = NonaConfig.create(NonaOptions("https://nona.test", "Production"), store, object : NonaHttpClient {
            override fun get(url: String, headers: Map<String, String>): NonaHttpResponse = error("Restore must not use HTTP")
        })
        assertFalse(config.initialize(), fixture.getString("baselineRef"))
    }

}
