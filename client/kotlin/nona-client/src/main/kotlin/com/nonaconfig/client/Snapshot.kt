package com.nonaconfig.client

import org.json.JSONException
import org.json.JSONObject

/** An immutable set of values, plus what is needed to revalidate it. */
internal data class Snapshot(
    val values: Map<String, NonaEntry>,
    val etag: String?,
    val fetchedAtMillis: Long,
) {
    companion object {

        /** Parses the `GET /api/{environmentId}` body: `{"key":{"value":..,"contentType":..}}`. */
        fun fromResponseBody(body: String, etag: String?, fetchedAtMillis: Long): Snapshot {
            val root = try {
                JSONObject(body)
            } catch (cause: JSONException) {
                throw NonaException("Nona returned an invalid config snapshot.", cause)
            }

            val values = LinkedHashMap<String, NonaEntry>(root.length())
            for (key in root.keys()) {
                val entry = root.optJSONObject(key)
                    ?: throw NonaException("Nona returned an invalid entry for '$key'.")
                val value = entry.opt("value") as? String
                    ?: throw NonaException("Nona returned a non-string value for '$key'.")
                val contentType = entry.opt("contentType") as? String ?: "text"
                values[key] = NonaEntry(value, contentType)
            }

            return Snapshot(values, etag, fetchedAtMillis)
        }

        fun fromCacheJson(json: String, identity: String): Snapshot? {
            return try {
                val root = JSONObject(json)
                if (root.optString("identity") != identity) return null
                val valuesJson = root.getJSONObject(FIELD_VALUES)
                val values = LinkedHashMap<String, NonaEntry>(valuesJson.length())
                for (key in valuesJson.keys()) {
                    val entry = valuesJson.getJSONObject(key)
                    values[key] = NonaEntry(
                        value = entry.getString(FIELD_VALUE),
                        contentType = entry.optString(FIELD_CONTENT_TYPE, "text"),
                    )
                }
                Snapshot(
                    values = values,
                    etag = root.optString(FIELD_ETAG).ifEmpty { null },
                    fetchedAtMillis = root.optLong(FIELD_FETCHED_AT),
                )
            } catch (_: JSONException) {
                // A corrupt cache is not worth failing over; refetch instead.
                null
            }
        }
    }

    fun toCacheJson(identity: String): String {
        val valuesJson = JSONObject()
        for ((key, entry) in values) {
            valuesJson.put(
                key,
                JSONObject()
                    .put(FIELD_VALUE, entry.value)
                    .put(FIELD_CONTENT_TYPE, entry.contentType),
            )
        }

        return JSONObject()
            .put("identity", identity)
            .put(FIELD_VALUES, valuesJson)
            .put(FIELD_ETAG, etag)
            .put(FIELD_FETCHED_AT, fetchedAtMillis)
            .toString()
    }
}

private const val FIELD_VALUES = "values"
private const val FIELD_VALUE = "value"
private const val FIELD_CONTENT_TYPE = "contentType"
private const val FIELD_ETAG = "etag"
private const val FIELD_FETCHED_AT = "fetchedAt"

/** Keys whose value or content type differ between two snapshots. */
internal fun changedKeys(previous: Map<String, NonaEntry>, next: Map<String, NonaEntry>): Set<String> =
    (previous.keys + next.keys).filterTo(LinkedHashSet()) { previous[it] != next[it] }
