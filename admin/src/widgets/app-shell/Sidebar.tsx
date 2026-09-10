import { A, useLocation, useNavigate } from "@solidjs/router";
import { useQuery } from "@tanstack/solid-query";
import { createMediaQuery } from "@solid-primitives/media";
import { For, Show } from "solid-js";
import { authService } from "../../entities/auth/api/auth.service";
import { canManageProjects, canManageUsers } from "../../entities/auth/model/permissions";
import { authStore } from "../../entities/auth/model/store";
import { projectService } from "../../entities/project/api/project.service";
import { getActiveProjectHref, getActiveProjectSlug } from "../../entities/project/model/active-project";
import { projectKeys } from "../../entities/project/queries/keys";
import { getProjectPageSection } from "../../shared/lib/project-navigation";
import { NonaMark } from "../../shared/ui/logo";

function getUser(): { email: string; role: string } {
  const session = authStore.getSession();
  return { email: session?.email ?? "", role: session?.role ?? "" };
}

interface NavItemDef {
  label: string;
  icon: string;
  href: () => string;
  isActive: () => boolean;
  requiresEdit?: boolean;
}

export const Sidebar = (props: {
  isOpen: boolean;
  onClose: () => void;
  collapsed: boolean;
  onToggleCollapse: () => void;
}) => {
  const location = useLocation();
  const navigate = useNavigate();
  const user = getUser();
  const isAdmin = canManageUsers();
  const canCreateProjects = canManageProjects();
  const initials = user.email ? user.email.slice(0, 2).toUpperCase() : "NA";

  const projectsQuery = useQuery(() => ({
    queryKey: projectKeys.list(),
    queryFn: () => projectService.getAll()
  }));
  // Once loaded, an empty instance collapses the nav to a single Create Project CTA.
  const noProjects = () => projectsQuery.isSuccess && (projectsQuery.data?.length ?? 0) === 0;
  const activeProject = () =>
    projectsQuery.data?.find(project => project.urlSlug === getActiveProjectSlug());
  const canEditActiveProject = () =>
    activeProject()?.accessLevel === "admin" || activeProject()?.accessLevel === "editor";

  const selectedProjectHref = () => getActiveProjectHref();
  const projectPageHref = (
    section: "environments" | "parameters" | "sharedLinks" | "apiKeys" | "releases"
  ) => {
    const projectHref = selectedProjectHref();
    if (projectHref === "/projects") return projectHref;
    if (section === "parameters") return projectHref;
    if (section === "environments") return `${projectHref}/environments`;
    if (section === "sharedLinks") return `${projectHref}/shared-links`;
    return `${projectHref}/${section === "apiKeys" ? "api-keys" : "releases"}`;
  };
  const activeProjectSection = () =>
    getProjectPageSection(location.pathname, location.search);

  const projectNavItems: NavItemDef[] = [
    {
      label: "Parameters",
      icon: "tune",
      href: () => projectPageHref("parameters"),
      isActive: () => activeProjectSection() === "parameters",
    },
    {
      label: "API Keys",
      icon: "key",
      href: () => projectPageHref("apiKeys"),
      isActive: () => activeProjectSection() === "apiKeys",
      requiresEdit: true,
    },
    {
      label: "Releases",
      icon: "deployed_code_history",
      href: () => projectPageHref("releases"),
      isActive: () => activeProjectSection() === "releases",
    },
    {
      label: "Shared Links",
      icon: "link",
      href: () => projectPageHref("sharedLinks"),
      isActive: () => activeProjectSection() === "sharedLinks",
      requiresEdit: true,
    },
    {
      label: "Environments",
      icon: "dns",
      href: () => projectPageHref("environments"),
      isActive: () => activeProjectSection() === "environments",
    },
  ];
  const visibleProjectNavItems = () =>
    projectNavItems.filter(item => !item.requiresEdit || canEditActiveProject());

  const footerNavItems: NavItemDef[] = [
    {
      label: "Projects",
      icon: "folder_open",
      href: () => "/projects",
      isActive: () => location.pathname === "/projects",
    },
    ...(isAdmin
      ? [
          {
            label: "Team",
            icon: "group",
            href: () => "/users",
            isActive: () =>
              location.pathname === "/users" || location.pathname.startsWith("/user"),
          },
          {
            label: "Audit Logs",
            icon: "manage_history",
            href: () => "/audit-logs",
            isActive: () => location.pathname === "/audit-logs",
          },
        ]
      : []),
  ];

  const navItem = (active: boolean, collapsed: boolean) =>
    `flex items-center gap-3 rounded-lg text-[14px] font-medium transition-all cursor-pointer ${collapsed ? "px-2.5 py-2.5 justify-center" : "px-3 py-2"
    } ${active
      ? "bg-primary/10 text-primary"
      : "text-on-surface-variant hover:bg-surface-container-low hover:text-on-surface"
    }`;

  const isDesktop = createMediaQuery("(min-width: 1024px)");
  const collapsed = () => props.collapsed && isDesktop();

  const w = () => (collapsed() ? "w-16" : "w-64");

  const handleProjectNavigation = (event: MouseEvent, href: string) => {
    props.onClose();
    if (
      event.defaultPrevented ||
      event.button !== 0 ||
      event.metaKey ||
      event.ctrlKey ||
      event.shiftKey ||
      event.altKey
    ) {
      return;
    }

    event.preventDefault();
    navigate(href);
  };

  return (
    <>
      <Show when={props.isOpen}>
        <div
          data-testid="sidebar-scrim"
          class="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 lg:hidden"
          onClick={() => props.onClose()}
        />
      </Show>

      <aside
        class={`h-screen ${w()} fixed left-0 top-0 bg-surface-container-lowest border-r border-outline-variant/20 flex flex-col z-60 sidebar-transition lg:translate-x-0 ${props.isOpen ? "translate-x-0" : "-translate-x-full"
          }`}
      >
        <div class={`pt-5 pb-4 flex items-start justify-between gap-2 ${collapsed() ? "px-3" : "px-4"}`}>
          <A
            href={selectedProjectHref()}
            onClick={() => props.onClose()}
            class="flex items-center gap-3 group"
          >
            <div class="w-8 h-8 rounded-lg shrink-0 flex items-center justify-center bg-primary/15 border border-primary/20 shadow-[0_0_12px_rgba(96,165,250,0.18)] group-hover:shadow-[0_0_20px_rgba(52,211,153,0.24)] transition-shadow duration-300">
              <NonaMark class="h-4.5 w-4.5 text-primary" />
            </div>
            <Show when={!collapsed()}>
              <div class="min-w-0">
                <p class="text-[15px] font-headline font-bold text-on-surface tracking-tight leading-none">
                  Nona Config
                </p>
                <p class="text-[10px] font-medium text-outline/50 tracking-[0.18em] uppercase mt-1">
                  Admin Console
                </p>
              </div>
            </Show>
          </A>

          <button
            type="button"
            data-testid="sidebar-close-button"
            onClick={() => props.onClose()}
            aria-label="Close navigation menu"
            class="lg:hidden -mr-1 -mt-1 flex shrink-0 cursor-pointer items-center justify-center rounded-lg border-0 bg-transparent p-2 text-on-surface-variant hover:text-on-surface focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
          >
            <span class="material-symbols-outlined text-xl">close</span>
          </button>
        </div>

        <div class="mx-3 h-px bg-outline-variant/20" />

        <div class={`pt-3 space-y-0.5 ${collapsed() ? "px-2" : "px-2"}`}>
          <Show
            when={noProjects()}
            fallback={
              <For each={visibleProjectNavItems()}>
                {item => (
                  <a
                    href={item.href()}
                    onClick={event => handleProjectNavigation(event, item.href())}
                    title={collapsed() ? item.label : undefined}
                    aria-label={item.label}
                    aria-current={item.isActive() ? "page" : undefined}
                    class={navItem(item.isActive(), collapsed())}
                  >
                    <span
                      class="material-symbols-outlined text-[20px] shrink-0"
                      style={
                        item.isActive()
                          ? "font-variation-settings: 'FILL' 1, 'wght' 400, 'GRAD' 0, 'opsz' 24"
                          : ""
                      }
                    >
                      {item.icon}
                    </span>
                    <Show when={!collapsed()}>{item.label}</Show>
                  </a>
                )}
              </For>
            }
          >
            <Show
              when={canCreateProjects}
              fallback={
                <A
                  href="/projects"
                  onClick={() => props.onClose()}
                  aria-label="Projects"
                  class={navItem(location.pathname === "/projects", collapsed())}
                >
                  <span class="material-symbols-outlined text-[20px] shrink-0">folder_open</span>
                  <Show when={!collapsed()}>Projects</Show>
                </A>
              }
            >
              <A
                href="/projects?new=1"
                onClick={() => props.onClose()}
                title={collapsed() ? "Create Project" : undefined}
                aria-label="Create Project"
                data-testid="sidebar-create-project"
                class={`bg-primary text-on-primary flex items-center justify-center gap-2 rounded-lg text-[14px] font-semibold transition-all hover:brightness-105 active:scale-[0.98] ${
                  collapsed() ? "px-2.5 py-2.5" : "px-3 py-2.5"
                }`}
              >
                <span class="material-symbols-outlined text-[18px] shrink-0">add</span>
                <Show when={!collapsed()}>Create Project</Show>
              </A>
            </Show>
          </Show>
        </div>

        <div class="flex-1" />

        <div class={`mt-auto pb-4 space-y-2 ${collapsed() ? "px-2" : "px-3"}`}>
          <Show when={!noProjects() || isAdmin}>
            <Show when={!collapsed()}>
              <p class="px-1 pb-1 text-[11px] font-semibold text-outline/50 tracking-[0.08em] uppercase">
                Admin
              </p>
            </Show>

            <div class="space-y-0.5">
              <For each={footerNavItems}>
                {item => (
                  <A
                    href={item.href()}
                    onClick={() => props.onClose()}
                    title={collapsed() ? item.label : undefined}
                    aria-label={item.label}
                    class={navItem(item.isActive(), collapsed())}
                  >
                    <span
                      class="material-symbols-outlined text-[20px] shrink-0"
                      style={
                        item.isActive()
                          ? "font-variation-settings: 'FILL' 1, 'wght' 400, 'GRAD' 0, 'opsz' 24"
                          : ""
                      }
                    >
                      {item.icon}
                    </span>
                    <Show when={!collapsed()}>{item.label}</Show>
                  </A>
                )}
              </For>
            </div>
          </Show>

          <button
            onClick={() => props.onToggleCollapse()}
            title={collapsed() ? "Expand sidebar" : "Collapse sidebar"}
            class={`hidden lg:flex w-full items-center gap-3 rounded-lg px-3 py-2 text-[13px] font-medium text-outline/60 hover:text-on-surface hover:bg-surface-container-low transition-all bg-transparent border-0 cursor-pointer ${collapsed() ? "justify-center" : ""
              }`}
          >
            <span
              class="material-symbols-outlined text-[18px] transition-transform duration-300"
              style={collapsed() ? "transform: rotate(180deg)" : ""}
            >
              left_panel_close
            </span>
            <Show when={!collapsed()}>
              <span>Collapse</span>
            </Show>
          </button>

          <Show when={authService.isAuthenticated() && !collapsed()}>
            <div class="rounded-xl border border-outline-variant/20 bg-surface-container-low flex items-center gap-3 p-3 hover:border-outline-variant/35 transition-all">
              <A
                href="/account"
                data-testid="sidebar-account-link"
                aria-label="Account settings"
                class="flex min-w-0 flex-1 items-center gap-3 rounded-lg"
              >
                <div class="w-8 h-8 rounded-lg shrink-0 flex items-center justify-center bg-primary/20 border border-primary/20">
                  <span class="text-[12px] font-headline font-bold text-primary">
                    {initials}
                  </span>
                </div>
                <div class="flex-1 min-w-0">
                  <p class="text-[13px] font-semibold text-on-surface truncate leading-tight">
                    {user.email || "Console User"}
                  </p>
                  <p class="text-[11px] text-outline/60 mt-0.5 capitalize tracking-wide">
                    {user.role || "member"}
                  </p>
                </div>
              </A>
              <button
                onClick={() => authService.logout()}
                title="Sign out"
                aria-label="Sign out"
                class="shrink-0 p-1.5 rounded-lg text-outline/50 hover:text-error hover:bg-error/8 transition-all bg-transparent border-0 cursor-pointer"
              >
                <span class="material-symbols-outlined text-[17px]">
                  logout
                </span>
              </button>
            </div>
          </Show>

          <Show when={authService.isAuthenticated() && collapsed()}>
            <div class="flex flex-col items-center gap-1.5">
              <A
                href="/account"
                aria-label="Account settings"
                title="Account settings"
                class="w-8 h-8 rounded-lg flex items-center justify-center bg-primary/20 border border-primary/20"
              >
                <span class="text-[12px] font-headline font-bold text-primary">
                  {initials}
                </span>
              </A>
              <button
                onClick={() => authService.logout()}
                title="Sign out"
                aria-label="Sign out"
                class="p-2 rounded-lg text-outline/50 hover:text-error hover:bg-error/8 transition-all bg-transparent border-0 cursor-pointer"
              >
                <span class="material-symbols-outlined text-[18px]">
                  logout
                </span>
              </button>
            </div>
          </Show>
        </div>
      </aside>
    </>
  );
};
