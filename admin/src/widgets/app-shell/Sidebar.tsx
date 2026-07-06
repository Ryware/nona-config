import { A, useLocation } from "@solidjs/router";
import { useQuery } from "@tanstack/solid-query";
import { For, Show } from "solid-js";
import { authService } from "../../entities/auth/api/auth.service";
import { authStore } from "../../entities/auth/model/store";
import { projectService } from "../../entities/project/api/project.service";
import { projectKeys } from "../../entities/project/queries/keys";

function getUser(): { email: string; role: string } {
  const session = authStore.getSession();
  return { email: session?.email ?? "", role: session?.isAdmin ? "admin" : (session?.role ?? "") };
}

interface NavItemDef {
  path: string;
  label: string;
  icon: string;
}

const mgmtNavItems: NavItemDef[] = [
  { path: "/dashboard", label: "Dashboard", icon: "dashboard" },
  { path: "/users", label: "Team", icon: "group" },
  { path: "/audit-logs", label: "Audit Logs", icon: "manage_history" },
];

export const Sidebar = (props: {
  isOpen: boolean;
  onClose: () => void;
  collapsed: boolean;
  onToggleCollapse: () => void;
}) => {
  const location = useLocation();
  const user = getUser();
  const initials = user.email ? user.email.slice(0, 2).toUpperCase() : "NA";

  const projectsQuery = useQuery(() => ({
    queryKey: projectKeys.list(),
    queryFn: () => projectService.getAll(),
  }));

  const isActive = (path: string) =>
    location.pathname === path || location.pathname.startsWith(path + "/");

  const navItem = (active: boolean, collapsed: boolean) =>
    `flex items-center gap-3 rounded-lg text-[13px] font-medium transition-all cursor-pointer ${
      collapsed ? "px-2.5 py-2.5 justify-center" : "px-3 py-2"
    } ${
      active
        ? "bg-primary/10 text-primary"
        : "text-on-surface-variant hover:bg-surface-container-low hover:text-on-surface"
    }`;

  const w = () => (props.collapsed ? "w-16" : "w-64");

  return (
    <>
      {/* Mobile overlay */}
      <Show when={props.isOpen}>
        <div
          class="fixed inset-0 bg-black/60 backdrop-blur-sm z-40 lg:hidden"
          onClick={() => props.onClose()}
        />
      </Show>

      <aside
        class={`h-screen ${w()} fixed left-0 top-0 bg-surface-container-lowest border-r border-outline-variant/20 flex flex-col z-50 sidebar-transition lg:translate-x-0 ${
          props.isOpen ? "translate-x-0" : "-translate-x-full"
        }`}
      >
        {/* Brand */}
        <div class={`pt-5 pb-4 ${props.collapsed ? "px-3" : "px-4"}`}>
          <A
            href="/projects"
            onClick={() => props.onClose()}
            class="flex items-center gap-3 group"
          >
            <div class="w-8 h-8 rounded-lg shrink-0 flex items-center justify-center bg-primary/15 border border-primary/20 shadow-[0_0_12px_rgba(99,102,241,0.15)] group-hover:shadow-[0_0_20px_rgba(99,102,241,0.25)] transition-shadow duration-300">
              <span
                class="material-symbols-outlined text-primary text-[18px]"
                style={{"font-variation-settings":"'FILL' 1, 'wght' 400, 'GRAD' 0, 'opsz' 24"}}
              >
                settings_input_component
              </span>
            </div>
            <Show when={!props.collapsed}>
              <div class="min-w-0">
                <p class="text-[14px] font-headline font-bold text-on-surface tracking-tight leading-none">
                  Nona Config
                </p>
                <p class="text-[9px] font-medium text-outline/50 tracking-[0.18em] uppercase mt-1">
                  Admin Console
                </p>
              </div>
            </Show>
          </A>
        </div>

        <div class="mx-3 h-px bg-outline-variant/20" />

        {/* Projects */}
        <div class={`pt-3 space-y-0.5 ${props.collapsed ? "px-2" : "px-2"}`}>
          <A
            href="/projects"
            onClick={() => props.onClose()}
            title={props.collapsed ? "Projects" : undefined}
            aria-label="Projects"
            class={navItem(isActive("/projects"), props.collapsed)}
          >
            <span
              class="material-symbols-outlined text-[20px] shrink-0"
              style={
                isActive("/projects")
                  ? "font-variation-settings: 'FILL' 1, 'wght' 400, 'GRAD' 0, 'opsz' 24"
                  : ""
              }
            >
              folder
            </span>
            <Show when={!props.collapsed}>
              <span class="flex-1">Projects</span>
              <Show when={projectsQuery.isSuccess}>
                <span class="text-[10px] font-mono text-outline/70 bg-surface-container-high/60 px-1.5 py-0.5 rounded-md leading-none">
                  {projectsQuery.data!.length}
                </span>
              </Show>
            </Show>
          </A>

          {/* Project sub-list */}
          <Show when={!props.collapsed}>
            <Show when={projectsQuery.isLoading}>
              <div class="pl-9 py-1 space-y-1">
                <div class="h-5 w-24 rounded-md bg-surface-container-high/40 animate-pulse" />
                <div class="h-5 w-16 rounded-md bg-surface-container-high/30 animate-pulse" />
              </div>
            </Show>
            <Show
              when={
                !projectsQuery.isLoading &&
                (projectsQuery.data?.length ?? 0) > 0
              }
            >
              <div class="pl-9 space-y-0.5 py-0.5">
                <For each={projectsQuery.data}>
                  {(project) => (
                    <A
                      href={`/projects/${project.urlSlug}`}
                      onClick={() => props.onClose()}
                      class={`flex items-center gap-2 px-2.5 py-1.5 rounded-lg text-[12px] font-medium truncate transition-all ${
                        isActive(`/projects/${project.urlSlug}`)
                          ? "text-primary bg-primary/8"
                          : "text-outline/80 hover:text-on-surface hover:bg-surface-container-low"
                      }`}
                    >
                      <span
                        class={`w-1 h-1 rounded-full shrink-0 ${
                          isActive(`/projects/${project.urlSlug}`)
                            ? "bg-primary"
                            : "bg-outline/40"
                        }`}
                      />
                      <span class="truncate">{project.name}</span>
                    </A>
                  )}
                </For>
              </div>
            </Show>
          </Show>
        </div>

        <div class="mx-3 h-px bg-outline-variant/20 mt-3" />

        {/* Management */}
        <nav
          class={`pt-3 space-y-0.5 flex-1 ${props.collapsed ? "px-2" : "px-2"}`}
        >
          <Show when={!props.collapsed}>
            <p class="px-3 pb-1 text-[10px] font-semibold text-outline/50 tracking-[0.08em] uppercase">
              Management
            </p>
          </Show>
          <For each={mgmtNavItems}>
            {(item) => (
              <A
                href={item.path}
                onClick={() => props.onClose()}
                title={props.collapsed ? item.label : undefined}
                aria-label={item.label}
                class={navItem(isActive(item.path), props.collapsed)}
              >
                <span
                  class="material-symbols-outlined text-[20px] shrink-0"
                  style={
                    isActive(item.path)
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
        </nav>

        {/* Bottom */}
        <div
          class={`mt-auto pb-4 space-y-2 ${props.collapsed ? "px-2" : "px-3"}`}
        >
          {/* Collapse toggle (desktop only) */}
          <button
            onClick={() => props.onToggleCollapse()}
            title={props.collapsed ? "Expand sidebar" : "Collapse sidebar"}
            class={`hidden lg:flex w-full items-center gap-3 rounded-lg px-3 py-2 text-[12px] font-medium text-outline/60 hover:text-on-surface hover:bg-surface-container-low transition-all bg-transparent border-0 cursor-pointer ${
              props.collapsed ? "justify-center" : ""
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

          {/* User card — expanded */}
          <Show when={authService.isAuthenticated() && !props.collapsed}>
            <div class="rounded-xl border border-outline-variant/20 bg-surface-container-low flex items-center gap-3 p-3 hover:border-outline-variant/35 transition-all">
              <div class="w-8 h-8 rounded-lg shrink-0 flex items-center justify-center bg-primary/20 border border-primary/20">
                <span class="text-[11px] font-headline font-bold text-primary">
                  {initials}
                </span>
              </div>
              <div class="flex-1 min-w-0">
                <p class="text-[12px] font-semibold text-on-surface truncate leading-tight">
                  {user.email || "Console User"}
                </p>
                <p class="text-[10px] text-outline/60 mt-0.5 capitalize tracking-wide">
                  {user.role || "editor"}
                </p>
              </div>
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

          {/* User card — collapsed */}
          <Show when={authService.isAuthenticated() && props.collapsed}>
            <div class="flex flex-col items-center gap-1.5">
              <div class="w-8 h-8 rounded-lg flex items-center justify-center bg-primary/20 border border-primary/20">
                <span class="text-[11px] font-headline font-bold text-primary">
                  {initials}
                </span>
              </div>
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
