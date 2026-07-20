import { createEventListener } from "@solid-primitives/event-listener";
import { MetaProvider, Title } from "@solidjs/meta";
import { Navigate, Route, Router, useLocation } from "@solidjs/router";
import { QueryClientProvider } from "@tanstack/solid-query";
import { type JSX, lazy, onMount, Show, Suspense } from "solid-js";
import { authService } from "../entities/auth/api/auth.service";
import { authStore } from "../entities/auth/model/store";
import { getActiveProjectHref } from "../entities/project/model/active-project";
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
                <Route path="/" component={HomeRoute} />

                <Route component={PublicRoute}>
                  <Route path="/login" component={lazy(() => import("../pages/auth/LoginPage"))} />
                  <Route
                    path="/register"
                    component={lazy(() => import("../pages/auth/RegisterPage"))}
                  />
                </Route>

                <Route
                  path="/cli-login"
                  component={lazy(() => import("../pages/auth/CliLoginPage"))}
                />

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
                    component={() => <Navigate href={getActiveProjectHref()} />}
                  />
                  <Route
                    path="/projects"
                    component={lazy(() => import("../pages/projects/ProjectsPage"))}
                  />
                  <Route
                    path="/projects/:slug/environments"
                    component={ProjectEnvironmentsPage}
                  />
                  <Route
                    path="/projects/:slug/shared-links"
                    component={ProjectShareLinksPage}
                  />
                  <Route path="/projects/:slug/api-keys" component={ProjectApiKeysPage} />
                  <Route path="/projects/:slug/releases" component={ProjectReleasesPage} />
                  <Route path="/projects/:slug" component={ProjectPage} />
                  <Route path="/users" component={lazy(() => import("../pages/users/UsersPage"))} />
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

// The project detail page and its section variants all live in one module, so
// they share a single lazily-loaded chunk (the dynamic import is cached).
const ProjectPage = lazy(() => import("../pages/projects/ProjectPage"));
const ProjectEnvironmentsPage = lazy(() =>
  import("../pages/projects/ProjectPage").then(module => ({ default: module.ProjectEnvironmentsPage }))
);
const ProjectApiKeysPage = lazy(() =>
  import("../pages/projects/ProjectPage").then(module => ({ default: module.ProjectApiKeysPage }))
);
const ProjectShareLinksPage = lazy(() =>
  import("../pages/projects/ProjectPage").then(module => ({ default: module.ProjectShareLinksPage }))
);
const ProjectReleasesPage = lazy(() =>
  import("../pages/projects/ProjectPage").then(module => ({ default: module.ProjectReleasesPage }))
);

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
    <Show when={!authService.isAuthenticated()} fallback={<Navigate href={getActiveProjectHref()} />}>
      <AuthLayout>{props.children}</AuthLayout>
    </Show>
  );
}

function InvitationRoute(props: { children?: JSX.Element }) {
  return <AuthLayout>{props.children}</AuthLayout>;
}

function HomeRoute() {
  return <Navigate href={authService.isAuthenticated() ? getActiveProjectHref() : "/projects"} />;
}
