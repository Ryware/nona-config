package com.nonaconfig.client

/**
 * Coercion from Nona's stored strings to typed values, kept deliberately in
 * step with the JavaScript providers so a value reads the same everywhere.
 */
internal object ValueParsing {

    fun parseBoolean(key: String, raw: String): ParseResult<Boolean> =
        when (raw.trim().lowercase()) {
            "true" -> ParseResult.Ok(true)
            "false" -> ParseResult.Ok(false)
            else -> mismatch(key, "boolean")
        }

    fun parseString(@Suppress("UNUSED_PARAMETER") key: String, raw: String): ParseResult<String> =
        ParseResult.Ok(raw)

    fun parseLong(key: String, raw: String): ParseResult<Long> =
        raw.trim().toLongOrNull()?.let { ParseResult.Ok(it) } ?: mismatch(key, "long")

    fun parseDouble(key: String, raw: String): ParseResult<Double> =
        raw.trim().toDoubleOrNull()?.takeIf(Double::isFinite)?.let { ParseResult.Ok(it) }
            ?: mismatch(key, "double")

    private fun mismatch(key: String, type: String) = ParseResult.Err(
        NonaFailureReason.TYPE_MISMATCH,
        "Nona flag '$key' cannot be evaluated as a $type.",
    )

    sealed interface ParseResult<out T> {
        data class Ok<out T>(val value: T) : ParseResult<T>
        data class Err(val reason: NonaFailureReason, val message: String) : ParseResult<Nothing>
    }
}
