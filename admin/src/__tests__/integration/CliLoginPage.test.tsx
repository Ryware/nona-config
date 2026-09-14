import { MetaProvider } from "@solidjs/meta";
import { Route, Router } from "@solidjs/router";
import { fireEvent, render, screen, waitFor } from "@solidjs/testing-library";
import { QueryClient, QueryClientProvider } from "@tanstack/solid-query";
import { beforeEach, describe, expect, it, vi } from "vitest";
import CliLoginPage from "../../pages/auth/CliLoginPage";
import { ThemeProvider } from "../../shared/hooks/useTheme";
import { ToastProvider } from "../../shared/ui/toast";
import { mockToken } from "../mocks/data";

function renderCliLogin(path: string) {
  window.history.pushState({}, "", path);
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } }
  });

  return render(() => (
    <MetaProvider>
      <ThemeProvider>
        <QueryClientProvider client={queryClient}>
          <ToastProvider>
            <Router>
              <Route path="/cli-login" component={CliLoginPage} />
            </Router>
          </ToastProvider>
        </QueryClientProvider>
      </ThemeProvider>
    </MetaProvider>
  ));
}

function cliLoginPath(state = "state-123", redirect = `${window.location.origin}/callback`) {
  return `/cli-login?cli_state=${encodeURIComponent(state)}&cli_redirect=${encodeURIComponent(redirect)}`;
}

describe("CliLoginPage", () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    vi.restoreAllMocks();
    window.history.pushState({}, "", "/");
  });

  it("requires consent before exporting an authenticated session", async () => {
    localStorage.setItem("auth_token", mockToken);
    localStorage.setItem(
      "auth_session",
      JSON.stringify({ email: "admin@example.com", role: "admin" })
    );

    renderCliLogin(cliLoginPath("state-auth"));
    const authorize = await screen.findByRole("button", { name: "Authorize CLI" });
    expect(window.location.pathname).toBe("/cli-login");
    expect(window.location.search).not.toContain("token=");
    fireEvent.click(authorize);

    await waitFor(() => {
      expect(window.location.pathname).toBe("/callback");
    });

    const callback = new URL(window.location.href);
    expect(callback.searchParams.get("token")).toBe(mockToken);
    expect(callback.searchParams.get("state")).toBe("state-auth");
    expect(callback.searchParams.get("username")).toBe("admin@example.com");
    expect(callback.searchParams.get("role")).toBe("admin");
    expect(callback.searchParams.get("expires_at")).toBeTruthy();
  });

  it("redirects to the CLI callback after password login", async () => {
    renderCliLogin(cliLoginPath("state-login"));

    fireEvent.input(screen.getByLabelText(/email/i), { target: { value: "admin@example.com" } });
    fireEvent.input(screen.getByLabelText(/^password$/i), { target: { value: "password" } });
    fireEvent.click(screen.getByRole("button", { name: /login to console/i }));
    const authorize = await screen.findByRole("button", { name: "Authorize CLI" });
    expect(window.location.pathname).toBe("/cli-login");
    fireEvent.click(authorize);

    await waitFor(() => {
      expect(window.location.pathname).toBe("/callback");
    });

    const callback = new URL(window.location.href);
    expect(callback.searchParams.get("token")).toBe(mockToken);
    expect(callback.searchParams.get("state")).toBe("state-login");
    expect(callback.searchParams.get("role")).toBe("admin");
    expect(callback.searchParams.get("expires_at")).toBe("2099-01-01T00:00:00Z");
  });

  it("cancels without disclosing a token", async () => {
    localStorage.setItem("auth_token", mockToken);
    renderCliLogin(cliLoginPath("cancel"));
    fireEvent.click(await screen.findByRole("button", { name: "Cancel" }));
    await waitFor(() => expect(window.location.pathname).toBe("/projects"));
    expect(window.location.search).not.toContain("token=");
  });

  it.each([
    "http://user@127.0.0.1/callback",
    "http://127.0.0.1/other",
    "https://127.0.0.1/callback"
  ])("rejects invalid callback %s", async redirect => {
    localStorage.setItem("auth_token", mockToken);
    renderCliLogin(cliLoginPath("invalid", redirect));
    expect(await screen.findByTestId("cli-login-error")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Authorize CLI" })).not.toBeInTheDocument();
  });

  it("rejects non-loopback CLI callback URLs", async () => {
    renderCliLogin(cliLoginPath("state-bad", "https://evil.example/callback"));

    expect(await screen.findByTestId("cli-login-error")).toBeInTheDocument();
    expect(screen.getByText(/loopback \/callback URL/i)).toBeInTheDocument();
    expect(window.location.pathname).toBe("/cli-login");
  });
});
