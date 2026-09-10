package com.nonaconfig.client

import java.io.File
import java.io.IOException

/**
 * Where the last good snapshot is kept so a cold start has values before the
 * network answers. Implementations must be thread-safe and treat cache failures
 * as best effort. Use an application-private location; the cache is not encrypted.
 */
interface NonaSnapshotStore {
    fun read(): String?
    fun write(json: String)
    fun clear()
}

/** Persists to a single file. Failures are swallowed: the cache is an optimisation. */
internal class FileSnapshotStore(private val file: File) : NonaSnapshotStore {

    @Synchronized
    override fun read(): String? = try {
        if (file.isFile) file.readText() else null
    } catch (_: IOException) {
        null
    }

    @Synchronized
    override fun write(json: String) {
        var temp: File? = null
        try {
            file.parentFile?.mkdirs()
            // Write beside the target and swap, so a kill mid-write cannot
            // leave a half-written cache behind.
            temp = File.createTempFile(file.name, ".tmp", file.parentFile)
            temp.outputStream().use { output ->
                output.write(json.toByteArray(Charsets.UTF_8))
                output.fd.sync()
            }
            // On failure retain the previous complete snapshot.
            temp.renameTo(file)
        } catch (_: IOException) {
            // Losing the cache costs one refetch.
        } finally {
            temp?.delete()
        }
    }

    @Synchronized
    override fun clear() {
        try {
            file.delete()
        } catch (_: SecurityException) {
            // Nothing useful to do.
        }
    }
}

/** In-memory store, for tests and for apps that do not want config on disk. */
class InMemorySnapshotStore : NonaSnapshotStore {
    @Volatile
    private var json: String? = null

    override fun read(): String? = json

    override fun write(json: String) {
        this.json = json
    }

    override fun clear() {
        json = null
    }
}
