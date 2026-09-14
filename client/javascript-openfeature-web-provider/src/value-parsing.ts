import type { NonaConfigValue } from "nona-client";

export type ParseFailureKind = "type-mismatch" | "parse-error";

export type ParseResult<T> =
  | { ok: true; value: T }
  | { ok: false; kind: ParseFailureKind; message: string };

export function parseBooleanValue(
  flagKey: string,
  config: NonaConfigValue,
): ParseResult<boolean> {
  const raw = config.value.trim().toLowerCase();
  if (raw === "true") {
    return { ok: true, value: true };
  }

  if (raw === "false") {
    return { ok: true, value: false };
  }

  return {
    ok: false,
    kind: "type-mismatch",
    message: `Nona flag '${flagKey}' cannot be evaluated as a boolean.`,
  };
}

export function parseStringValue(
  _flagKey: string,
  config: NonaConfigValue,
): ParseResult<string> {
  return { ok: true, value: config.value };
}

export function parseNumberValue(
  flagKey: string,
  config: NonaConfigValue,
): ParseResult<number> {
  const value = Number(config.value);
  if (config.value.trim() === "" || !Number.isFinite(value)) {
    return {
      ok: false,
      kind: "type-mismatch",
      message: `Nona flag '${flagKey}' cannot be evaluated as a number.`,
    };
  }

  return { ok: true, value };
}

export function parseObjectValue<T>(
  flagKey: string,
  config: NonaConfigValue,
): ParseResult<T> {
  try {
    return { ok: true, value: JSON.parse(config.value) as T };
  } catch {
    return {
      ok: false,
      kind: "parse-error",
      message: `Nona flag '${flagKey}' cannot be parsed as JSON.`,
    };
  }
}
