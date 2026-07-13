const API_KEY_HEADER_NAME = "X-Api-Key";

export function applyAuthentication(headers: Headers, apiKey?: string): void {
  if (!apiKey?.trim()) {
    throw new Error("Nona API-key calls require createNonaClient(...).apiKey.");
  }

  headers.set(API_KEY_HEADER_NAME, apiKey);
}
