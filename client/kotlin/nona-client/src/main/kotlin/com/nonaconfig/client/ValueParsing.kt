package com.nonaconfig.client

/**
 * Coercion from Nona's stored strings, checked against the shared mobile SDK
 * contract. Numbers use finite decimal syntax, without language-specific suffixes.
 */
internal object ValueParsing {
    private val integer = Regex("[+-]?[0-9]+")
    private val decimal = Regex("[+-]?(?:[0-9]+(?:\\.[0-9]*)?|\\.[0-9]+)(?:[eE][+-]?[0-9]+)?")

    fun parseBoolean(key: String, raw: String): ParseResult<Boolean> =
        when (raw.trim().lowercase()) {
            "true" -> ParseResult.Ok(true)
            "false" -> ParseResult.Ok(false)
            else -> mismatch(key, "boolean")
        }

    fun parseString(@Suppress("UNUSED_PARAMETER") key: String, raw: String): ParseResult<String> =
        ParseResult.Ok(raw)

    fun parseLong(key: String, raw: String): ParseResult<Long> =
        raw.trim().takeIf { integer.matches(it) }?.toLongOrNull()?.let { ParseResult.Ok(it) } ?: mismatch(key, "long")

    fun parseDouble(key: String, raw: String): ParseResult<Double> =
        raw.trim().takeIf { decimal.matches(it) }?.toDoubleOrNull()?.takeIf(Double::isFinite)?.let { ParseResult.Ok(it) }
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
