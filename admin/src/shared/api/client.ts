const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || window.location.origin;

const ALLOW_401_ENDPOINTS = [
  "/auth/login",
  "/auth/first-time",
  "/auth/sso/google",
  "/auth/sso/microsoft",
  "/auth/sso/config",
];

function isAllowlisted401Endpoint(endpoint: string) {
  return ALLOW_401_ENDPOINTS.includes(endpoint) || endpoint.startsWith("/auth/invitations/");
}

export class ApiRequestError extends Error {
  code?: string;

  constructor(message: string, code?: string) {
    super(message);
    this.name = "ApiRequestError";
    this.code = code;
  }
}

export class ApiClient {
  private getAuthHeader(): HeadersInit {
    const token = localStorage.getItem("auth_token") || sessionStorage.getItem("auth_token");
    return token ? { Authorization: `Bearer ${token}` } : {};
  }

  async request<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
    const url = `${API_BASE_URL}${endpoint}`;

    const response = await fetch(url, {
      ...options,
      headers: {
        "Content-Type": "application/json",
        ...this.getAuthHeader(),
        ...options.headers,
      },
    });

    if (!response.ok) {
      if (response.status === 401 && !isAllowlisted401Endpoint(endpoint)) {
        // Signal the auth store to clear the session and redirect.
        // Using a custom event keeps this shared module free of entity-layer imports.
        window.dispatchEvent(new CustomEvent("auth:unauthorized"));
      }

      const error = await response
        .json()
        .catch(() => ({ detail: "An error occurred" }));
      throw new ApiRequestError(
        error.detail || error.error || error.message || error.title || "Request failed",
        error.errorCode,
      );
    }

    // Handle 204 No Content
    if (response.status === 204) {
      return {} as T;
    }

    return response.json();
  }

  async get<T>(endpoint: string): Promise<T> {
    return this.request<T>(endpoint, { method: "GET" });
  }

  async post<T>(endpoint: string, data?: unknown): Promise<T> {
    return this.request<T>(endpoint, {
      method: "POST",
      body: data ? JSON.stringify(data) : undefined,
    });
  }

  async put<T>(endpoint: string, data?: unknown): Promise<T> {
    return this.request<T>(endpoint, {
      method: "PUT",
      body: data ? JSON.stringify(data) : undefined,
    });
  }

  async delete<T>(endpoint: string): Promise<T> {
    return this.request<T>(endpoint, { method: "DELETE" });
  }
}

export const apiClient = new ApiClient();
