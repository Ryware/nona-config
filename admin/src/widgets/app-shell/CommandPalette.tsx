import { useNavigate } from "@solidjs/router";
import { useQuery } from "@tanstack/solid-query";
import {
  type JSX,
  createMemo,
  createSignal,
  For,
  on,
  createEffect,
  onMount,
  Show,
} from "solid-js";
import { projectService } from "../../entities/project/api/project.service";
import { projectKeys } from "../../entities/project/queries/keys";
import { userService } from "../../entities/user/api/user.service";
import { userKeys } from "../../entities/user/queries/keys";
import { Input } from "../../shared/ui/input";

interface CommandPaletteProps {
  onClose: () => void;
}

type ResultItem =
  | { kind: "nav"; label: string; icon: string; path: string; desc: string }
  | { kind: "project"; id: string; name: string; slug: string; path: string }
  | { kind: "user"; id: string; name: string; email: string };

interface PaletteItemProps {
  isActive: boolean;
  onClick: () => void;
  onMouseEnter: () => void;
  children: JSX.Element;
}

function PaletteItem(props: PaletteItemProps) {
  return (
    <button
      onClick={() => props.onClick()}
      onMouseEnter={() => props.onMouseEnter()}
      class={`w-full flex items-center gap-3 px-4 py-2.5 text-left transition-colors cursor-pointer bg-transparent border-0 select-none ${
        props.isActive
          ? "bg-primary/10 text-primary"
          : "text-on-surface-variant hover:bg-surface-container-high hover:text-on-surface"
      }`}
    >
      {props.children}
    </button>
  );
}

export const CommandPalette = (props: CommandPaletteProps) => {
  const navigate = useNavigate();
  const [query, setQuery] = createSignal("");
  const [activeIdx, setActiveIdx] = createSignal(0);
  let inputRef: HTMLInputElement | undefined;

  const projectsQuery = useQuery(() => ({
    queryKey: projectKeys.list(),
    queryFn: () => projectService.getAll(),
  }));

  const usersQuery = useQuery(() => ({
    queryKey: userKeys.list(),
    queryFn: () => userService.getAll(),
  }));

  const quickNav = [
    {
      label: "All Projects",
      icon: "folder",
      path: "/projects",
      desc: "View all configuration projects",
    },
    {
      label: "Team Management",
      icon: "group",
      path: "/users",
      desc: "Manage team members and roles",
    },
    {
      label: "Audit Logs",
      icon: "history",
      path: "/audit-logs",
      desc: "Review recent system activity",
    },
  ];

  const filteredProjects = createMemo(() => {
    const q = query().toLowerCase().trim();
    const projects = projectsQuery.status === 'success' ? projectsQuery.data ?? [] : [];
    if (!q) return projects.slice(0, 5);
    return projects
      .filter(
        (p) =>
          p.name.toLowerCase().includes(q) ||
          p.urlSlug.toLowerCase().includes(q) ||
          (p.description ?? "").toLowerCase().includes(q)
      )
      .slice(0, 5);
  });

  const filteredUsers = createMemo(() => {
    const q = query().toLowerCase().trim();
    if (!q) return [];
    const users = usersQuery.status === 'success' ? usersQuery.data ?? [] : [];
    return users
      .filter(
        (u) =>
          u.email.toLowerCase().includes(q) ||
          (u.name ?? "").toLowerCase().includes(q)
      )
      .slice(0, 3);
  });

  const filteredNav = createMemo(() => {
    const q = query().toLowerCase().trim();
    if (!q) return quickNav;
    return quickNav.filter(
      (n) => n.label.toLowerCase().includes(q) || n.desc.toLowerCase().includes(q)
    );
  });

  const allResults = createMemo<ResultItem[]>(() => {
    const results: ResultItem[] = [];
    for (const n of filteredNav()) results.push({ kind: "nav", ...n });
    for (const p of filteredProjects())
      results.push({
        kind: "project",
        id: p.id,
        name: p.name,
        slug: p.urlSlug,
        path: `/projects/${p.urlSlug}`,
      });
    for (const u of filteredUsers())
      results.push({
        kind: "user",
        id: u.id,
        name: u.name ?? "",
        email: u.email,
      });
    return results;
  });

  // Reset active index on query change (on() tracks only the listed signals)
  createEffect(on(query, () => { setActiveIdx(0); }));

  onMount(() => {
    inputRef?.focus();
  });

  const navigateTo = (path: string) => {
    navigate(path);
    props.onClose();
  };

  const activateItem = (item: ResultItem) => {
    if (item.kind === "nav" || item.kind === "project") navigateTo(item.path);
    else if (item.kind === "user") navigateTo("/users");
  };

  const handleKeyDown = (e: KeyboardEvent) => {
    const results = allResults();
    if (e.key === "Escape") {
      props.onClose();
    } else if (e.key === "ArrowDown") {
      e.preventDefault();
      setActiveIdx((i) => Math.min(i + 1, results.length - 1));
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setActiveIdx((i) => Math.max(i - 1, 0));
    } else if (e.key === "Enter") {
      e.preventDefault();
      const item = results[activeIdx()];
      if (item) activateItem(item);
    }
  };

  return (
    <div
      class="fixed inset-0 z-100 flex items-start justify-center pt-[15vh] animate-backdrop-in"
      style={{ "background": "rgba(0,0,0,0.75)", "backdrop-filter": "blur(6px)" }}
      onClick={() => props.onClose()}
    >
      <div
        class="w-full max-w-lg mx-4 bg-surface-container-low border border-outline-variant/25 rounded-2xl shadow-2xl overflow-hidden animate-palette-in"
        onClick={(e) => e.stopPropagation()}
        onKeyDown={handleKeyDown}
      >
        {/* Search input */}
        <div class="flex items-center gap-3 px-4 py-3.5 border-b border-outline-variant/15">
          <span class="material-symbols-outlined text-outline text-[22px] shrink-0">
            search
          </span>
          <Input
            ref={inputRef}
            type="text"
            placeholder="Search projects, team members…"
            value={query()}
            onInput={(e) => setQuery(e.currentTarget.value)}
            class="flex-1 bg-transparent border-0 rounded-none h-auto py-0 px-0 text-sm hover:border-transparent focus:border-transparent focus:ring-0"
          />
          <kbd class="hidden sm:flex items-center px-1.5 py-0.5 text-[10px] font-bold text-outline bg-surface-container-high border border-outline-variant/20 rounded-md select-none">
            ESC
          </kbd>
        </div>

        {/* Results */}
        <div class="max-h-[min(22rem,60vh)] overflow-y-auto">
          {/* Default Navigation Section */}
          <Show when={!query()}>
            <p class="px-4 pt-3 pb-1 text-[10px] font-medium text-outline/70 tracking-[0.05em] select-none">
              Quick Navigate
            </p>
            <For each={filteredNav()}>
              {(item, i) => (
                <PaletteItem
                  isActive={activeIdx() === i()}
                  onClick={() => navigateTo(item.path)}
                  onMouseEnter={() => setActiveIdx(i())}
                >
                  <span class="material-symbols-outlined text-[20px] shrink-0">
                    {item.icon}
                  </span>
                  <div>
                    <p class="text-[13px] font-medium leading-snug">{item.label}</p>
                    <p class="text-[11px] text-outline">{item.desc}</p>
                  </div>
                </PaletteItem>
              )}
            </For>
          </Show>

          {/* Projects Results Section */}
          <Show when={query() && filteredProjects().length > 0}>
            <p class="px-4 pt-3 pb-1 text-[10px] font-medium text-outline/70 tracking-[0.05em] select-none">
              Projects
            </p>
            <For each={filteredProjects()}>
              {(project, i) => {
                const globalIdx = () => filteredNav().length + i();
                return (
                  <PaletteItem
                    isActive={activeIdx() === globalIdx()}
                    onClick={() => navigateTo(`/projects/${project.urlSlug}`)}
                    onMouseEnter={() => setActiveIdx(globalIdx())}
                  >
                    <div class="w-7 h-7 rounded-lg bg-primary/10 border border-primary/20 flex items-center justify-center shrink-0">
                      <span class="material-symbols-outlined text-[16px] text-primary">
                        folder
                      </span>
                    </div>
                    <div>
                      <p class="text-[13px] font-medium text-on-surface leading-snug">
                        {project.name}
                      </p>
                      <p class="text-[11px] font-mono text-outline">{project.urlSlug}</p>
                    </div>
                  </PaletteItem>
                );
              }}
            </For>
          </Show>

          {/* Users Results Section */}
          <Show when={query() && filteredUsers().length > 0}>
            <p class="px-4 pt-3 pb-1 text-[10px] font-medium text-outline/70 tracking-[0.05em] select-none">
              Team Members
            </p>
            <For each={filteredUsers()}>
              {(user, i) => {
                const globalIdx = () => filteredNav().length + filteredProjects().length + i();
                const initials = (user.name || user.email).slice(0, 2).toUpperCase();
                return (
                  <PaletteItem
                    isActive={activeIdx() === globalIdx()}
                    onClick={() => navigateTo("/users")}
                    onMouseEnter={() => setActiveIdx(globalIdx())}
                  >
                    <div class="w-7 h-7 rounded-full bg-primary/10 border border-primary/20 flex items-center justify-center text-[11px] font-bold text-primary shrink-0">
                      {initials}
                    </div>
                    <div>
                      <p class="text-[13px] font-medium text-on-surface leading-snug">
                        {user.name || "Pending Invite"}
                      </p>
                      <p class="text-[11px] text-outline">{user.email}</p>
                    </div>
                  </PaletteItem>
                );
              }}
            </For>
          </Show>

          {/* Empty Results view */}
          <Show when={query() && allResults().length === 0}>
            <div class="py-10 text-center select-none">
              <span class="material-symbols-outlined text-4xl text-outline block mb-2">
                search_off
              </span>
              <p class="text-sm text-on-surface-variant">
                No results for <span class="text-on-surface font-medium">"{query()}"</span>
              </p>
            </div>
          </Show>
        </div>

        {/* Footer shortcuts helper */}
        <div class="border-t border-outline-variant/15 px-4 py-2 flex items-center gap-5 text-[10px] text-outline select-none">
          <span class="flex items-center gap-1.5">
            <kbd class="bg-surface-container-high border border-outline-variant/20 rounded px-1 py-0.5 font-bold">
              ↑↓
            </kbd>
            Navigate
          </span>
          <span class="flex items-center gap-1.5">
            <kbd class="bg-surface-container-high border border-outline-variant/20 rounded px-1 py-0.5 font-bold">
              ↵
            </kbd>
            Open
          </span>
          <span class="flex items-center gap-1.5">
            <kbd class="bg-surface-container-high border border-outline-variant/20 rounded px-1 py-0.5 font-bold">
              ESC
            </kbd>
            Close
          </span>
        </div>
      </div>
    </div>
  );
};
