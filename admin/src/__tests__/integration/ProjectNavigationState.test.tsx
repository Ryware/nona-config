import { MetaProvider } from "@solidjs/meta";
import { Route, Router } from "@solidjs/router";
import { fireEvent, render, screen, waitFor, within } from "@solidjs/testing-library";
import { QueryClient, QueryClientProvider } from "@tanstack/solid-query";
import { beforeEach, describe, expect, it } from "vitest";

import { setActiveProjectSlug } from "../../entities/project/model/active-project";
import ParametersSection from "../../pages/projects/sections/ParametersSection";
import { UnsavedChangesProvider } from "../../shared/hooks/useUnsavedChanges";
import { ToastProvider } from "../../shared/ui/toast";
import { Breadcrumbs } from "../../widgets/app-shell/Breadcrumbs";
import { Sidebar } from "../../widgets/app-shell/Sidebar";
import { mockToken } from "../mocks/data";

function renderProjectNavigation(path: string) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } }
  });
  window.history.pushState({}, "", path);

  return render(() => (
    <MetaProvider>
      <QueryClientProvider client={queryClient}>
        <Router>
          <Route
            path="*"
            component={() => (
              <>
                <Sidebar
                  isOpen
                  onClose={() => undefined}
                  collapsed={false}
                  onToggleCollapse={() => undefined}
                />
                <Breadcrumbs />
              </>
            )}
          />
        </Router>
      </QueryClientProvider>
    </MetaProvider>
  ));
}

function renderReleaseDraftNavigation(path: string) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } }
  });
  window.history.pushState({}, "", path);

  return render(() => (
    <MetaProvider>
      <QueryClientProvider client={queryClient}>
        <ToastProvider>
          <Router
            root={props => (
              <UnsavedChangesProvider>
                <Sidebar
                  isOpen
                  onClose={() => undefined}
                  collapsed={false}
                  onToggleCollapse={() => undefined}
                />
                {props.children}
              </UnsavedChangesProvider>
            )}
          >
            <Route path="/projects/:slug" component={ParametersSection} />
          </Router>
        </ToastProvider>
      </QueryClientProvider>
    </MetaProvider>
  ));
}

describe("project navigation state", () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    localStorage.setItem("auth_token", mockToken);
    localStorage.setItem(
      "auth_session",
      JSON.stringify({ email: "admin@example.com", role: "admin" })
    );
    setActiveProjectSlug("my-app");
  });

  it.each([
    "/projects/my-app/releases",
    "/projects/my-app?viewRelease=1.2.0",
    "/projects/my-app?release=1.3.0",
    "/projects/my-app?release=1.3.1&amend=1.3.0"
  ])("keeps Releases current throughout %s", async path => {
    renderProjectNavigation(path);

    const releasesLink = await screen.findByRole("link", { name: "Releases" });
    const parametersLink = screen.getByRole("link", { name: "Parameters" });
    expect(releasesLink).toHaveAttribute("aria-current", "page");
    expect(parametersLink).not.toHaveAttribute("aria-current");
    expect(within(screen.getByRole("navigation")).getByText("Releases")).toBeInTheDocument();
  });

  it("navigates from release context to live Parameters and updates the current section", async () => {
    renderProjectNavigation("/projects/my-app?viewRelease=1.2.0");

    const parametersLink = await screen.findByRole("link", { name: "Parameters" });
    fireEvent.click(parametersLink);

    await waitFor(() => {
      expect(window.location.pathname).toBe("/projects/my-app");
      expect(window.location.search).toBe("");
      expect(parametersLink).toHaveAttribute("aria-current", "page");
    });
    expect(screen.getByRole("link", { name: "Releases" })).not.toHaveAttribute("aria-current");
    expect(within(screen.getByRole("navigation")).getByText("Parameters")).toBeInTheDocument();
  });

  it("keeps release creation current while its exit confirmation is open", async () => {
    renderReleaseDraftNavigation("/projects/my-app?release=1.3.0");

    const parametersLink = await screen.findByRole("link", { name: "Parameters" });
    const releasesLink = screen.getByRole("link", { name: "Releases" });
    await screen.findByTestId("release-create-confirm-button");
    fireEvent.click(parametersLink);

    expect(await screen.findByTestId("release-exit-dialog")).toBeInTheDocument();
    expect(window.location.search).toBe("?release=1.3.0");
    expect(releasesLink).toHaveAttribute("aria-current", "page");

    fireEvent.click(screen.getByTestId("release-exit-cancel-button"));
    expect(window.location.search).toBe("?release=1.3.0");

    fireEvent.click(parametersLink);
    fireEvent.click(await screen.findByTestId("release-exit-confirm-button"));

    await waitFor(() => {
      expect(window.location.pathname).toBe("/projects/my-app");
      expect(window.location.search).toBe("");
      expect(parametersLink).toHaveAttribute("aria-current", "page");
    });
  });
});
