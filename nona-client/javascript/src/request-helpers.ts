export function segment(value: string, parameterName: string): string {
  if (!value?.trim()) {
    throw new Error(`${parameterName} cannot be empty.`);
  }

  return encodeURIComponent(value);
}

export function ensureTrailingSlash(url: URL): URL {
  const value = url.toString();
  return value.endsWith("/") ? url : new URL(`${value}/`);
}

export function resolveNonNegativeNumber(
  input: number | undefined
): number {
  if (!Number.isFinite(input) || +input! < 0) {
    throw new Error(`Values provided in options must be a non-negative finite number.`);
  }

  return input!;
}

export function buildRequestKey(
  baseUrl: URL,
  method: string,
  path: string,
  apiKey?: string,
): string {
  const normalizedPath = path.replace(/^\/+/, "");
  const fullUrl = new URL(normalizedPath, baseUrl).toString();
  const authToken = apiKey?.trim() ?? "";
  return `${method.toUpperCase()}\u0000${fullUrl}\u0000${authToken}`;
}
