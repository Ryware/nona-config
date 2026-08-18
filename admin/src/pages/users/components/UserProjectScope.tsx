import { For, Show } from "solid-js";
import type { Project } from "../../../types";
import { Tooltip, TooltipTrigger } from "../../../shared/ui/tooltip";
import { tooltipCopy } from "../../../shared/lib/tooltip-copy";

interface UserProjectScopeProps {
  projects: Project[];
  projectAccess: Record<string, "editor" | "viewer">;
  onChange: (projectName: string, role: "editor" | "viewer" | null) => void;
}

export function UserProjectScope(props: UserProjectScopeProps) {
  return (
    <section class="bg-surface-container-low border-outline-variant/15 space-y-5 rounded-xl border p-4 shadow-sm sm:p-8">
      <div class="flex items-center gap-3">
        <div class="bg-primary/10 border-primary/20 text-primary flex h-7 w-7 shrink-0 items-center justify-center rounded-full border font-mono text-xs font-bold shadow-[0_0_12px_rgba(99,102,241,0.15)]">
          03
        </div>
        <Tooltip content={`${tooltipCopy.viewer} ${tooltipCopy.editor}`}>
          <TooltipTrigger as="h3" tabindex="0" data-tooltip-trigger class="font-headline text-on-surface cursor-help border-b border-dotted border-outline/60 text-lg font-bold">Project Scope</TooltipTrigger>
        </Tooltip>
      </div>
      <div class="bg-surface-container-low border-outline-variant/15 overflow-hidden rounded-xl border">
        <div class="bg-surface-container-low border-outline-variant/15 text-outline hidden grid-cols-2 border-b px-6 py-3.5 text-[10px] font-bold tracking-widest uppercase sm:grid">
          <span>Active Projects</span>
          <span class="text-right">Access Level</span>
        </div>
        <Show when={props.projects.length === 0}>
          <div class="text-outline px-6 py-8 text-center text-sm">No projects found</div>
        </Show>
        <div class="divide-outline-variant/10 divide-y">
          <For each={props.projects}>
            {project => {
              const projectName = project.name || project.urlSlug;
              return (
                <div
                  data-testid={`invite-project-row-${project.urlSlug}`}
                  class="hover:bg-surface-container-high/40 border-outline-variant/10 grid gap-3 border-b px-4 py-4 transition-colors last:border-b-0 sm:grid-cols-2 sm:items-center sm:px-6"
                >
                  <div class="flex items-center gap-3">
                    <span class="text-on-surface font-mono text-sm font-semibold">
                      {project.urlSlug}
                    </span>
                  </div>
                  <div class="sm:text-right">
                    <select
                      data-testid={`invite-project-${project.urlSlug}`}
                      aria-label={`Access level for project ${project.name || project.urlSlug}`}
                      value={props.projectAccess[projectName] ?? "none"}
                      onChange={event =>
                        props.onChange(
                          projectName,
                          event.currentTarget.value === "none"
                            ? null
                            : (event.currentTarget.value as "editor" | "viewer")
                        )
                      }
                      class="bg-surface-container-low border-outline-variant/20 text-on-surface h-9 rounded-lg border px-3 text-sm"
                    >
                      <option value="none">None</option>
                      <option value="viewer">Viewer</option>
                      <option value="editor">Editor</option>
                    </select>
                  </div>
                </div>
              );
            }}
          </For>
        </div>
      </div>
    </section>
  );
}
