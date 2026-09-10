import { captureSession, sessionToken } from "./session";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || window.location.origin;

const ALLOW_401_ENDPOINTS = [
  "/auth/login",
  "/auth/first-time",
  "/auth/sso/google",
  "/auth/sso/microsoft",
  "/auth/sso/config"
];

function isAllowlisted401Endpoint(endpoint: string) {
  return (
    ALLOW_401_ENDPOINTS.includes(endpoint) ||
    endpoint.startsWith("/auth/invitations/") ||
    endpoint.startsWith("/auth/password-resets/")
  );
}

export class ApiRequestError extends Error {
  code?: string;

  constructor(message: string, code?: string) {
    super(message);
    this.name = "ApiRequestError";
    this.code = code;
  }
}

function getValidationErrorMessage(payload: unknown): string | undefined {
  if (!payload || typeof payload !== "object" || Array.isArray(payload)) return undefined;

  const errors = (payload as { errors?: unknown }).errors;
  if (!errors || typeof errors !== "object" || Array.isArray(errors)) return undefined;

  const messages = Object.entries(errors)
    .sort(([left], [right]) => left.localeCompare(right))
    .flatMap(([field, values]) =>
      Array.isArray(values)
        ? values
            .filter(
              (value): value is string => typeof value === "string" && value.trim().length > 0
            )
            .map(value => `${field}: ${value}`)
        : []
    );

  return messages.length > 0 ? messages.join("; ") : undefined;
}

function getErrorMessage(payload: unknown): string {
  const validationMessage = getValidationErrorMessage(payload);
  if (validationMessage) return validationMessage;

  if (!payload || typeof payload !== "object" || Array.isArray(payload)) return "Request failed";

  const error = payload as Record<string, unknown>;
  const fallbackFields = [error.detail, error.error, error.message, error.title];
  return (
    fallbackFields.find(
      (value): value is string => typeof value === "string" && value.trim().length > 0
    ) ?? "Request failed"
  );
}

export class ApiClient {
  private getAuthHeader(): HeadersInit {
    const token = sessionToken();
    return token ? { Authorization: `Bearer ${token}` } : {};
  }

  private async send(
    endpoint: string,
    options: RequestInit,
    assertSession: () => void
  ): Promise<Response> {
    const url = `${API_BASE_URL}${endpoint}`;

    const response = await fetch(url, {
      ...options,
      headers: {
        "Content-Type": "application/json",
        ...this.getAuthHeader(),
        ...options.headers
      }
    });

    assertSession();
    if (!response.ok) {
      if (response.status === 401 && !isAllowlisted401Endpoint(endpoint)) {
        // Signal the auth store to clear the session and redirect.
        // Using a custom event keeps this shared module free of entity-layer imports.
        window.dispatchEvent(new CustomEvent("auth:unauthorized"));
      }

      const error = await response.json().catch(() => ({ detail: "An error occurred" }));
      assertSession();
      throw new ApiRequestError(getErrorMessage(error), error.errorCode);
    }

    return response;
  }

  async request<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
    const assertSession = captureSession();
    const response = await this.send(endpoint, options, assertSession);

    // Handle 204 No Content
    if (response.status === 204) {
      return {} as T;
    }

    const data = await response.json();
    assertSession();
    return data;
  }

  async getBlob(endpoint: string): Promise<{ blob: Blob; fileName?: string }> {
    const assertSession = captureSession();
    const response = await this.send(endpoint, { method: "GET" }, assertSession);
    const disposition = response.headers.get("Content-Disposition");
    const encodedFileName = disposition?.match(/filename\*?=(?:UTF-8''|")?([^";]+)/i)?.[1];

    const blob = await response.blob();
    assertSession();
    return {
      blob,
      fileName: encodedFileName ? decodeURIComponent(encodedFileName) : undefined
    };
  }

  async get<T>(endpoint: string): Promise<T> {
    return this.request<T>(endpoint, { method: "GET" });
  }

  async post<T>(endpoint: string, data?: unknown): Promise<T> {
    return this.request<T>(endpoint, {
      method: "POST",
      body: data ? JSON.stringify(data) : undefined
    });
  }

  async put<T>(endpoint: string, data?: unknown): Promise<T> {
    return this.request<T>(endpoint, {
      method: "PUT",
      body: data ? JSON.stringify(data) : undefined
    });
  }

  async delete<T>(endpoint: string): Promise<T> {
    return this.request<T>(endpoint, { method: "DELETE" });
  }
}

export const apiClient = new ApiClient();
