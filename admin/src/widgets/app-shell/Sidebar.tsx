import { A, useLocation } from "@solidjs/router";
import { useQuery } from "@tanstack/solid-query";
import { For, Show } from "solid-js";
import { authService } from "../../entities/auth/api/auth.service";
import { canManageProjects, canManageUsers } from "../../entities/auth/model/permissions";
import { authStore } from "../../entities/auth/model/store";
import { projectService } from "../../entities/project/api/project.service";
import { getActiveProjectHref, getActiveProjectSlug } from "../../entities/project/model/active-project";
import { projectKeys } from "../../entities/project/queries/keys";
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
  const isProjectPage = () => location.pathname.startsWith("/projects/") && location.pathname !== "/projects";

  const projectNavItems: NavItemDef[] = [
    {
      label: "Parameters",
      icon: "tune",
      href: () => projectPageHref("parameters"),
      isActive: () =>
        isProjectPage() &&
        !location.pathname.endsWith("/environments") &&
        !location.pathname.endsWith("/shared-links") &&
        !location.pathname.endsWith("/api-keys") &&
        !location.pathname.endsWith("/releases"),
    },
    {
      label: "API Keys",
      icon: "key",
      href: () => projectPageHref("apiKeys"),
      isActive: () => location.pathname.endsWith("/api-keys"),
      requiresEdit: true,
    },
    {
      label: "Releases",
      icon: "deployed_code_history",
      href: () => projectPageHref("releases"),
      isActive: () => location.pathname.endsWith("/releases"),
    },
    {
      label: "Shared Links",
      icon: "link",
      href: () => projectPageHref("sharedLinks"),
      isActive: () => location.pathname.endsWith("/shared-links"),
      requiresEdit: true,
    },
    {
      label: "Environments",
      icon: "dns",
      href: () => projectPageHref("environments"),
      isActive: () => location.pathname.endsWith("/environments"),
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

  const w = () => (props.collapsed ? "w-16" : "w-64");

  return (
    <>
      <Show when={props.isOpen}>
        <div
          class="fixed inset-0 bg-black/60 backdrop-blur-sm z-40 lg:hidden"
          onClick={() => props.onClose()}
        />
      </Show>

      <aside
        class={`h-screen ${w()} fixed left-0 top-0 bg-surface-container-lowest border-r border-outline-variant/20 flex flex-col z-50 sidebar-transition lg:translate-x-0 ${props.isOpen ? "translate-x-0" : "-translate-x-full"
          }`}
      >
        <div class={`pt-5 pb-4 ${props.collapsed ? "px-3" : "px-4"}`}>
          <A
            href={selectedProjectHref()}
            onClick={() => props.onClose()}
            class="flex items-center gap-3 group"
          >
            <div class="w-8 h-8 rounded-lg shrink-0 flex items-center justify-center bg-primary/15 border border-primary/20 shadow-[0_0_12px_rgba(96,165,250,0.18)] group-hover:shadow-[0_0_20px_rgba(52,211,153,0.24)] transition-shadow duration-300">
              <NonaMark class="h-4.5 w-4.5 text-primary" />
            </div>
            <Show when={!props.collapsed}>
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
        </div>

        <div class="mx-3 h-px bg-outline-variant/20" />

        <div class={`pt-3 space-y-0.5 ${props.collapsed ? "px-2" : "px-2"}`}>
          <Show
            when={noProjects()}
            fallback={
              <For each={visibleProjectNavItems()}>
                {item => (
                  <A
                    href={item.href()}
                    onClick={() => props.onClose()}
                    title={props.collapsed ? item.label : undefined}
                    aria-label={item.label}
                    class={navItem(item.isActive(), props.collapsed)}
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
                    <Show when={!props.collapsed}>{item.label}</Show>
                  </A>
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
                  class={navItem(location.pathname === "/projects", props.collapsed)}
                >
                  <span class="material-symbols-outlined text-[20px] shrink-0">folder_open</span>
                  <Show when={!props.collapsed}>Projects</Show>
                </A>
              }
            >
              <A
                href="/projects?new=1"
                onClick={() => props.onClose()}
                title={props.collapsed ? "Create Project" : undefined}
                aria-label="Create Project"
                data-testid="sidebar-create-project"
                class={`bg-primary text-on-primary flex items-center justify-center gap-2 rounded-lg text-[14px] font-semibold transition-all hover:brightness-105 active:scale-[0.98] ${
                  props.collapsed ? "px-2.5 py-2.5" : "px-3 py-2.5"
                }`}
              >
                <span class="material-symbols-outlined text-[18px] shrink-0">add</span>
                <Show when={!props.collapsed}>Create Project</Show>
              </A>
            </Show>
          </Show>
        </div>

        <div class="flex-1" />

        <div class={`mt-auto pb-4 space-y-2 ${props.collapsed ? "px-2" : "px-3"}`}>
          <Show when={!noProjects() || isAdmin}>
            <Show when={!props.collapsed}>
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
                    title={props.collapsed ? item.label : undefined}
                    aria-label={item.label}
                    class={navItem(item.isActive(), props.collapsed)}
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
                    <Show when={!props.collapsed}>{item.label}</Show>
                  </A>
                )}
              </For>
            </div>
          </Show>

          <button
            onClick={() => props.onToggleCollapse()}
            title={props.collapsed ? "Expand sidebar" : "Collapse sidebar"}
            class={`hidden lg:flex w-full items-center gap-3 rounded-lg px-3 py-2 text-[13px] font-medium text-outline/60 hover:text-on-surface hover:bg-surface-container-low transition-all bg-transparent border-0 cursor-pointer ${props.collapsed ? "justify-center" : ""
              }`}
          >
            <span
              class="material-symbols-outlined text-[18px] transition-transform duration-300"
              style={props.collapsed ? "transform: rotate(180deg)" : ""}
            >
              left_panel_close
            </span>
            <Show when={!props.collapsed}>
              <span>Collapse</span>
            </Show>
          </button>

          <Show when={authService.isAuthenticated() && !props.collapsed}>
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

          <Show when={authService.isAuthenticated() && props.collapsed}>
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
