import type { ConfigEntry } from "../../types";

export function isValidConfigEntryValue(
  contentType: ConfigEntry["contentType"],
  value: string,
): boolean {
  const trimmed = value.trim();

  if (trimmed.length === 0) {
    return false;
  }

  if (contentType === "text") {
    return true;
  }

  try {
    const parsed = JSON.parse(trimmed) as unknown;

    if (contentType === "json") {
      return true;
    }

    if (contentType === "number") {
      return typeof parsed === "number" && Number.isFinite(parsed);
    }

    return typeof parsed === "boolean";
  } catch {
    return false;
  }
}
