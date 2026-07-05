import { A, useLocation } from "@solidjs/router";
import { For, Show } from "solid-js";

interface Crumb {
  label: string;
  path?: string;
}

function parseBreadcrumbs(pathname: string): Crumb[] {
  const parts = pathname.split("/").filter(Boolean);
  if (parts.length === 0) return [];

  if (parts[0] === "projects") {
    const base: Crumb = {
      label: "Projects",
      path: "/projects",
    };
    if (parts[1]) {
      return [base, { label: parts[1] }];
    }
    return [base];
  }
  if (parts[0] === "users" || parts[0] === "user") {
    const base: Crumb = { label: "Team", path: "/users" };
    if (parts[0] === "user" && !parts[1]) {
      return [base, { label: "Invite Member" }];
    }
    if (parts[0] === "user") {
      return [base, { label: "Edit Member" }];
    }
    return [base];
  }
  if (parts[0] === "invite") {
    return [{ label: "Invitation" }];
  }
  if (parts[0] === "audit-logs") {
    return [{ label: "Audit Logs", path: "/audit-logs" }];
  }
  return [];
}

export function Breadcrumbs() {
  const location = useLocation();
  const crumbs = () => parseBreadcrumbs(location.pathname);

  return (
    <nav class="flex-1 flex items-center gap-2 text-[12px] min-w-0 overflow-hidden font-medium">
      <Show when={crumbs().length > 0}>
        <For each={crumbs()}>
          {(crumb, i) => (
            <>
              <Show when={i() > 0}>
                <span class="text-outline/40 shrink-0 select-none font-normal">
                  &gt;
                </span>
              </Show>
              <Show
                when={crumb.path && i() < crumbs().length - 1}
                fallback={
                  <span class="text-on-surface font-semibold truncate select-none">
                    {crumb.label}
                  </span>
                }
              >
                <A
                  href={crumb.path!}
                  class="text-on-surface-variant hover:text-primary transition-colors truncate"
                >
                  {crumb.label}
                </A>
              </Show>
            </>
          )}
        </For>
      </Show>
    </nav>
  );
}
