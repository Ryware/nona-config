import type { ConfigEntry } from "../../types";

export type ConfigEntryContentType = ConfigEntry["contentType"];

export interface ConfigEntryDraftValidation {
  isValid: boolean;
  keyError?: string;
  valueError?: string;
  disabledReason?: string;
}

interface ConfigEntryDraft {
  key: string;
  value: string;
  contentType: ConfigEntryContentType;
  existingKeys?: readonly string[];
}

const CONFIG_ENTRY_KEY_ALLOWED_CHAR = /[A-Za-z0-9:._-]/;
const CONFIG_ENTRY_KEY_DISALLOWED_CHARS = /[^A-Za-z0-9:._-]/g;

export function sanitizeConfigEntryKey(key: string): string {
  return key.replace(CONFIG_ENTRY_KEY_DISALLOWED_CHARS, "");
}

export function isDisallowedConfigEntryKeyPress(event: KeyboardEvent): boolean {
  if (event.ctrlKey || event.metaKey || event.altKey) {
    return false;
  }

  return event.key.length === 1 && !CONFIG_ENTRY_KEY_ALLOWED_CHAR.test(event.key);
}

export function readConfigEntryKeyInput(input: HTMLInputElement): string {
  const raw = input.value;
  const sanitized = sanitizeConfigEntryKey(raw);

  if (sanitized === raw) {
    return raw;
  }

  const caret = input.selectionStart ?? raw.length;
  const removedBeforeCaret =
    raw.slice(0, caret).length - sanitizeConfigEntryKey(raw.slice(0, caret)).length;

  input.value = sanitized;
  const nextCaret = Math.max(0, caret - removedBeforeCaret);
  input.setSelectionRange(nextCaret, nextCaret);

  return sanitized;
}

const CONFIG_ENTRY_KEY_ERROR =
  "Key must contain an ASCII letter or digit and may only contain ASCII letters, digits, colons, dots, underscores, and dashes.";

export function validateConfigEntryKey(
  key: string,
  existingKeys: readonly string[],
): string | undefined {
  const trimmedKey = key.trim();

  if (!trimmedKey) {
    return "Parameter key is required.";
  }

  if (!/[A-Za-z0-9]/.test(trimmedKey) || !/^[A-Za-z0-9:._-]+$/.test(trimmedKey)) {
    return CONFIG_ENTRY_KEY_ERROR;
  }

  const segments = trimmedKey.split(":");
  if (segments.length > 4 || segments.some(segment => segment.length === 0)) {
    return "Parameter key must contain between one and four non-empty colon-separated segments.";
  }

  if (existingKeys.some((existingKey) => existingKey.toLowerCase() === trimmedKey.toLowerCase())) {
    return "Parameter key already exists. Keys are case-insensitive.";
  }

  return undefined;
}

export function getConfigEntryValueError(
  contentType: ConfigEntryContentType,
  value: string,
): string | undefined {
  if (contentType === "text") {
    return undefined;
  }

  const trimmed = value.trim();
  if (!trimmed) {
    if (contentType === "boolean") {
      return "Select True or False.";
    }

    return `Enter a ${contentType === "json" ? "JSON" : "number"} value.`;
  }

  try {
    const parsed = JSON.parse(trimmed) as unknown;

    if (contentType === "json") {
      return undefined;
    }

    if (contentType === "number") {
      return typeof parsed === "number" && Number.isFinite(parsed)
        ? undefined
        : "Value must be a valid JSON number.";
    }

    return typeof parsed === "boolean"
      ? undefined
      : "Value must be 'true' or 'false'.";
  } catch (caught) {
    if (contentType === "json") {
      const reason = caught instanceof Error ? caught.message : "The value could not be parsed.";
      return `Invalid JSON: ${reason}`;
    }

    if (contentType === "number") {
      return "Value must be a valid JSON number.";
    }

    return "Value must be 'true' or 'false'.";
  }
}

export function validateConfigEntryDraft({
  key,
  value,
  contentType,
  existingKeys = [],
}: ConfigEntryDraft): ConfigEntryDraftValidation {
  const keyError = validateConfigEntryKey(key, existingKeys);
  const valueError = getConfigEntryValueError(contentType, value);
  const isValid = !keyError && !valueError;

  return {
    isValid,
    keyError,
    valueError,
    disabledReason: isValid
      ? undefined
      : keyError === "Parameter key is required."
        ? "Enter a parameter key to enable this action."
        : keyError ?? valueError,
  };
}

export function isValidConfigEntryValue(
  contentType: ConfigEntry["contentType"],
  value: string,
): boolean {
  return !getConfigEntryValueError(contentType, value);
}
