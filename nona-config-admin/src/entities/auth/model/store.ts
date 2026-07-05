/**
 * Centralized auth session management.
 * Single source of truth for token storage and session metadata.
 *
 * Security note: tokens currently stored in localStorage/sessionStorage.
 * TODO: Migrate to httpOnly cookie once backend supports it (issue: backend/auth-cookie).
 */

export interface AuthSession {
  email: string;
  role: string;
  username?: string;
  isAdmin?: boolean;
}

export const authStore = {
  /**
   * Persist token and session metadata after a successful login.
   * @param rememberMe - true → localStorage, false → sessionStorage (cleared on tab close)
   */
  saveSession(token: string, session: AuthSession, rememberMe = true): void {
    const storage = rememberMe ? localStorage : sessionStorage;
    storage.setItem("auth_token", token);
    storage.setItem(
      "auth_session",
      JSON.stringify({
        ...session,
        isAdmin: session.isAdmin ?? getIsAdminClaim(token)
      })
    );
  },

  clearSession(): void {
    localStorage.removeItem("auth_token");
    sessionStorage.removeItem("auth_token");
    localStorage.removeItem("auth_session");
    sessionStorage.removeItem("auth_session");
  },

  getToken(): string | null {
    return localStorage.getItem("auth_token") ?? sessionStorage.getItem("auth_token");
  },

  getSession(): AuthSession | null {
    try {
      const raw = localStorage.getItem("auth_session") ?? sessionStorage.getItem("auth_session");
      if (raw) {
        const session = JSON.parse(raw) as AuthSession;
        return {
          ...session,
          isAdmin: session.isAdmin ?? getIsAdminClaim(this.getToken() ?? "")
        };
      }
    } catch {
      // corrupted storage — treat as logged out
    }
    return null;
  },

  isAuthenticated(): boolean {
    if (!this.getToken()) return false;
    if (this.isTokenExpired()) {
      this.clearSession();
      return false;
    }
    return true;
  },

  /**
   * Decodes the JWT payload (base64url) and checks whether the `exp` claim
   * has passed. Returns `true` when the token is expired or malformed.
   */
  isTokenExpired(): boolean {
    const token = this.getToken();
    if (!token) return true;
    try {
      const parts = token.split(".");
      // Not a standard JWT — treat as non-expired (e.g. opaque tokens)
      if (parts.length !== 3) return false;
      // base64url → base64 padding
      const payload = parts[1].replace(/-/g, "+").replace(/_/g, "/");
      const decoded = JSON.parse(atob(payload)) as Record<string, unknown>;
      if (typeof decoded.exp !== "number") return false;
      return decoded.exp < Date.now() / 1000;
    } catch {
      return false;
    }
  },

  /**
   * Called when the API receives an unexpected 401.
   * Clears the session and redirects to the login page.
   */
  handleUnauthorized(): void {
    this.clearSession();
    window.location.href = "/login";
  }
};

function getIsAdminClaim(token: string): boolean {
  try {
    const parts = token.split(".");
    if (parts.length !== 3) return false;

    const payload = parts[1].replace(/-/g, "+").replace(/_/g, "/");
    const decoded = JSON.parse(atob(payload)) as Record<string, unknown>;
    const claim = decoded.isAdmin;

    return claim === true || claim === "true";
  } catch {
    return false;
  }
}
