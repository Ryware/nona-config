import { createEventListener } from "@solid-primitives/event-listener";
import { Title } from "@solidjs/meta";
import { createRouter, useLocation, useNavigate } from "@solidjs/router";
import { QueryClientProvider } from "@tanstack/solid-query";
import { lazy, Loading, onSettled, Show } from "solid-js";
import type { JSX } from "@solidjs/web";
import { authService } from "../entities/auth/api/auth.service";
import { canManageUsers } from "../entities/auth/model/permissions";
import { authStore } from "../entities/auth/model/store";
import { getActiveProjectHref } from "../entities/project/model/active-project";
import { ThemeProvider } from "../shared/hooks/useTheme";
import { UnsavedChangesProvider } from "../shared/hooks/useUnsavedChanges";
import { AccessDenied } from "../shared/ui/AccessDenied";
import { RouteLoader } from "../shared/ui/Skeleton";
import { ToastProvider } from "../shared/ui/toast";
import { queryClient } from "./query-client";

export default function App(): JSX.Element {
  if (typeof window !== "undefined") {
    // Keep shared/api/client.ts free of entity-layer imports.
    createEventListener(window, "auth:unauthorized", () => authStore.handleUnauthorized());
    createEventListener(window, "storage", event => authStore.handleStorageChange(event));
  }

  return (
    <ThemeProvider>
        <Title>Nona Config Admin</Title>
        <QueryClientProvider client={queryClient}>
          <ToastProvider>
            <Loading
              fallback={
                <>
                  <RouteLoader />
                </>
              }
            >
              <Router>{props => props.children}</Router>
            </Loading>
          </ToastProvider>
        </QueryClientProvider>
    </ThemeProvider>
  );
}

const ParametersSection = lazy(() => import("../pages/projects/sections/ParametersSection"));
const EnvironmentsSection = lazy(() => import("../pages/projects/sections/EnvironmentsSection"));
const ApiKeysSection = lazy(() => import("../pages/projects/sections/ApiKeysSection"));
const SharedLinksSection = lazy(() => import("../pages/projects/sections/SharedLinksSection"));
const ReleasesSection = lazy(() => import("../pages/projects/sections/ReleasesSection"));

const AppLayout = lazy(() =>
  import("../widgets/app-shell/AppLayout").then(module => ({ default: module.AppLayout }))
);

// Protected route layout
function ProtectedRoute(props: { children?: JSX.Element }) {
  const location = useLocation();

  return (
    <Show
      when={authService.isAuthenticated()}
      fallback={<Redirect href={`/login?redirect=${encodeURIComponent(location.pathname)}`} />}
    >
      <UnsavedChangesProvider>
        <AppLayout>{props.children}</AppLayout>
      </UnsavedChangesProvider>
    </Show>
  );
}

function AdminRoute(props: { children?: JSX.Element }) {
  return (
    <Show when={canManageUsers()} fallback={<AccessDenied />}>
      {props.children}
    </Show>
  );
}

const AuthLayout = lazy(() =>
  import("../widgets/auth-shell/AuthLayout").then(module => ({ default: module.AuthLayout }))
);

// Public route layout (redirect to dashboard if already authenticated)
function PublicRoute(props: { children?: JSX.Element }) {
  return (
    <Show
      when={!authService.isAuthenticated()}
      fallback={<Redirect href={getActiveProjectHref()} />}
    >
      <AuthLayout>{props.children}</AuthLayout>
    </Show>
  );
}

function InvitationRoute(props: { children?: JSX.Element }) {
  return <AuthLayout>{props.children}</AuthLayout>;
}

function HomeRoute() {
  return <Redirect href={authService.isAuthenticated() ? getActiveProjectHref() : "/projects"} />;
}

function Redirect(props: { href: string }) {
  const navigate = useNavigate();
  onSettled(() => navigate(props.href, { replace: true }));
  return null;
}

const Router = createRouter({
  routes: [
    { path: "/", component: HomeRoute },
    {
      component: PublicRoute,
      children: [
        { path: "/login", component: lazy(() => import("../pages/auth/LoginPage")) },
        { path: "/register", component: lazy(() => import("../pages/auth/RegisterPage")) },
      ],
    },
    { path: "/cli-login", component: lazy(() => import("../pages/auth/CliLoginPage")) },
    { path: "/sso/callback/:provider", component: lazy(() => import("../pages/auth/SsoCallbackPage")) },
    { path: "/invite/:token", component: InvitationRoute, children: [
      { path: "/", component: lazy(() => import("../pages/auth/InvitePage")) },
    ] },
    { path: "/reset-password/:token", component: InvitationRoute, children: [
      { path: "/", component: lazy(() => import("../pages/auth/ResetPasswordPage")) },
    ] },
    { path: "/share/:token", component: lazy(() => import("../pages/shared/SharedParameterPage")) },
    {
      component: ProtectedRoute,
      children: [
        { path: "/dashboard", component: () => <Redirect href={getActiveProjectHref()} /> },
        { path: "/projects", component: lazy(() => import("../pages/projects/ProjectsPage")) },
        { path: "/projects/:slug/environments", component: EnvironmentsSection },
        { path: "/projects/:slug/shared-links", component: SharedLinksSection },
        { path: "/projects/:slug/api-keys", component: ApiKeysSection },
        { path: "/projects/:slug/releases", component: ReleasesSection },
        { path: "/projects/:slug", component: ParametersSection },
        { path: "/account", component: lazy(() => import("../pages/account/AccountPage")) },
        { component: AdminRoute, children: [
          { path: "/users", component: lazy(() => import("../pages/users/UsersPage")) },
          { path: "/audit-logs", component: lazy(() => import("../pages/audit-logs/AuditLogsPage")) },
        ] },
      ],
    },
  ],
});
