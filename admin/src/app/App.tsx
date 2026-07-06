import { createEventListener } from "@solid-primitives/event-listener";
import { MetaProvider, Title } from "@solidjs/meta";
import { Navigate, Route, Router, useLocation } from "@solidjs/router";
import { QueryClientProvider } from "@tanstack/solid-query";
import { type JSX, lazy, onMount, Show, Suspense } from "solid-js";
import { authService } from "../entities/auth/api/auth.service";
import { authStore } from "../entities/auth/model/store";
import { ThemeProvider } from "../shared/hooks/useTheme";
import { RouteLoader } from "../shared/ui/Skeleton";
import { ToastProvider } from "../shared/ui/toast";
import { queryClient } from "./query-client";

export default function App(): JSX.Element {
  onMount(() => {
    if (window) {
      // Listen for unauthorized events dispatched by the API client.
      // Using a custom event keeps shared/api/client.ts free of entity-layer imports.
      createEventListener(window, "auth:unauthorized", () => authStore.handleUnauthorized());
    }
  });

  return (
    <MetaProvider>
      <ThemeProvider>
        <Title>Nona Config Admin</Title>
        <QueryClientProvider client={queryClient}>
          <ToastProvider>
            <Suspense
              fallback={
                <>
                  <RouteLoader />
                </>
              }
            >
              <Router>
                <Route path="/" component={() => <Navigate href="/projects" />} />

                <Route component={PublicRoute}>
                  <Route path="/login" component={lazy(() => import("../pages/auth/LoginPage"))} />
                  <Route
                    path="/register"
                    component={lazy(() => import("../pages/auth/RegisterPage"))}
                  />
                </Route>

                <Route component={InvitationRoute}>
                  <Route
                    path="/invite/:token"
                    component={lazy(() => import("../pages/auth/InvitePage"))}
                  />
                </Route>

                <Route
                  path="/share/:token"
                  component={lazy(() => import("../pages/shared/SharedParameterPage"))}
                />

                <Route component={ProtectedRoute}>
                  <Route
                    path="/dashboard"
                    component={lazy(() => import("../pages/dashboard/DashboardPage"))}
                  />
                  <Route
                    path="/projects"
                    component={lazy(() => import("../pages/projects/ProjectsPage"))}
                  />
                  <Route
                    path="/projects/:slug"
                    component={lazy(() => import("../pages/projects/ProjectPage"))}
                  />
                  <Route path="/users" component={lazy(() => import("../pages/users/UsersPage"))} />
                  <Route path="/user" component={lazy(() => import("../pages/users/UserPage"))} />
                  <Route
                    path="/audit-logs"
                    component={lazy(() => import("../pages/audit-logs/AuditLogsPage"))}
                  />
                </Route>
              </Router>
            </Suspense>
          </ToastProvider>
        </QueryClientProvider>
      </ThemeProvider>
    </MetaProvider>
  );
}

const AppLayout = lazy(() =>
  import("../widgets/app-shell/AppLayout").then(module => ({ default: module.AppLayout }))
);

// Protected route layout
function ProtectedRoute(props: { children?: JSX.Element }) {
  const location = useLocation();

  return (
    <Show
      when={authService.isAuthenticated()}
      fallback={<Navigate href={`/login?redirect=${encodeURIComponent(location.pathname)}`} />}
    >
      <AppLayout>{props.children}</AppLayout>
    </Show>
  );
}

const AuthLayout = lazy(() =>
  import("../widgets/auth-shell/AuthLayout").then(module => ({ default: module.AuthLayout }))
);

// Public route layout (redirect to dashboard if already authenticated)
function PublicRoute(props: { children?: JSX.Element }) {
  return (
    <Show when={!authService.isAuthenticated()} fallback={<Navigate href="/projects" />}>
      <AuthLayout>{props.children}</AuthLayout>
    </Show>
  );
}

function InvitationRoute(props: { children?: JSX.Element }) {
  return <AuthLayout>{props.children}</AuthLayout>;
}
