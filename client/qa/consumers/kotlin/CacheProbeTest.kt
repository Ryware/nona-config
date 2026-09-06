package com.nonaconfig.compat

import com.nonaconfig.client.*
import java.io.File
import kotlin.test.*
import kotlin.time.Duration
import kotlinx.coroutines.runBlocking

/** Loaded by the compatibility runner into each revision's test source set. */
class CacheProbeTest {
    @Test fun cacheAcrossVersions() = runBlocking<Unit> {
        val file = File(requireNotNull(System.getenv("NONA_COMPAT_CACHE")))
        val store = object : NonaSnapshotStore {
            override fun read() = if (file.isFile) file.readText() else null
            override fun write(json: String) { file.writeText(json) }
            override fun clear() { file.delete() }
        }
        val config = NonaConfig.create(NonaOptions("https://nona.test", "Production", minimumFetchInterval = Duration.ZERO), store,
            object : NonaHttpClient {
                override fun get(url: String, headers: Map<String, String>) = NonaHttpResponse(200, """{"flag":{"value":"compatible"}}""", null)
            })
        if (System.getenv("NONA_COMPAT_MODE") == "seed") config.fetch()
        else {
            assertTrue(config.initialize())
            assertEquals("compatible", config.getString("flag"))
        }
    }
}
