import { onMount, Show } from "solid-js";
import { authStore } from "../../entities/auth/model/store";
import type { LoginResponse } from "../../types";
import { AuthCard } from "../../widgets/auth-shell/AuthCard";
import { AuthLayout } from "../../widgets/auth-shell/AuthLayout";
import LoginPage from "./LoginPage";

interface CliLoginRequest {
  state: string;
  redirectUrl: URL;
}

interface CliLoginCallback {
  token: string;
  username: string;
  role: string;
  expiresAt: string;
}

export default function CliLoginPage() {
  const parsed = parseCliLoginRequest(window.location.search);

  onMount(() => {
    if (!parsed.request || !authStore.isAuthenticated()) return;

    const token = authStore.getToken();
    if (!token) return;

    const session = authStore.getSession();
    redirectToCliCallback(parsed.request, {
      token,
      username: session?.username ?? session?.email ?? "",
      role: session?.role ?? "",
      expiresAt: inferExpiresAt(token)
    });
  });

  return (
    <Show
      when={!parsed.error}
      fallback={
        <AuthLayout>
          <AuthCard
            title="CLI Login"
            error={parsed.error}
            testId="cli-login-error"
            headingTestId="cli-login-heading"
          >
            <p class="text-on-surface-variant text-center text-[12.5px]">
              Return to your terminal and run the login command again.
            </p>
          </AuthCard>
        </AuthLayout>
      }
    >
      <Show when={!authStore.isAuthenticated()} fallback={null}>
        <AuthLayout>
          <LoginPage
            onLoginSuccess={(result: LoginResponse) =>
              redirectToCliCallback(parsed.request!, {
                token: result.token,
                username: result.username ?? "",
                role: result.role,
                expiresAt: result.expiresAt
              })
            }
          />
        </AuthLayout>
      </Show>
    </Show>
  );
}

function parseCliLoginRequest(search: string): { request?: CliLoginRequest; error?: string } {
  const parameters = new URLSearchParams(search);
  const state = parameters.get("cli_state")?.trim();
  const rawRedirect = parameters.get("cli_redirect");

  if (!state || !rawRedirect) {
    return { error: "CLI login request is missing required callback information." };
  }

  let redirectUrl: URL;
  try {
    redirectUrl = new URL(rawRedirect);
  } catch {
    return { error: "CLI login callback URL is invalid." };
  }

  if (!isAllowedCliRedirect(redirectUrl)) {
    return { error: "CLI login callback must be a loopback /callback URL." };
  }

  return { request: { state, redirectUrl } };
}

function isAllowedCliRedirect(url: URL) {
  const host = url.hostname.toLowerCase();
  return (
    url.protocol === "http:" &&
    url.username === "" &&
    url.password === "" &&
    url.pathname === "/callback" &&
    (host === "localhost" || host === "127.0.0.1" || host === "::1" || host === "[::1]")
  );
}

function redirectToCliCallback(request: CliLoginRequest, callback: CliLoginCallback) {
  const redirectUrl = new URL(request.redirectUrl.toString());
  redirectUrl.searchParams.set("token", callback.token);
  redirectUrl.searchParams.set("state", request.state);
  redirectUrl.searchParams.set("username", callback.username);
  redirectUrl.searchParams.set("role", callback.role);
  redirectUrl.searchParams.set("expires_at", callback.expiresAt);
  window.location.href = redirectUrl.toString();
}

function inferExpiresAt(token: string) {
  try {
    const parts = token.split(".");
    if (parts.length === 3) {
      const payload = parts[1].replace(/-/g, "+").replace(/_/g, "/");
      const decoded = JSON.parse(atob(payload)) as Record<string, unknown>;
      if (typeof decoded.exp === "number") {
        return new Date(decoded.exp * 1000).toISOString();
      }
    }
  } catch {
    // Fall through to the same fallback used by the CLI callback receiver.
  }

  return new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString();
}
