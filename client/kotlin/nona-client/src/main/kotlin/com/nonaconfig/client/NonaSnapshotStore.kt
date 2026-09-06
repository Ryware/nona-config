package com.nonaconfig.client

import java.io.File
import java.io.IOException

/**
 * Where the last good snapshot is kept so a cold start has values before the
 * network answers.
 */
interface NonaSnapshotStore {
    fun read(): String?
    fun write(json: String)
    fun clear()
}

/** Persists to a single file. Failures are swallowed: the cache is an optimisation. */
internal class FileSnapshotStore(private val file: File) : NonaSnapshotStore {

    override fun read(): String? = try {
        if (file.isFile) file.readText() else null
    } catch (_: IOException) {
        null
    }

    override fun write(json: String) {
        try {
            file.parentFile?.mkdirs()
            // Write beside the target and swap, so a kill mid-write cannot
            // leave a half-written cache behind.
            val temp = File(file.parentFile, "${file.name}.tmp")
            temp.writeText(json)
            if (!temp.renameTo(file)) {
                file.writeText(json)
                temp.delete()
            }
        } catch (_: IOException) {
            // Losing the cache costs one refetch.
        }
    }

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
    private var json: String? = null

    override fun read(): String? = json

    override fun write(json: String) {
        this.json = json
    }

    override fun clear() {
        json = null
    }
}
