import { For, Show } from "solid-js";
import type { Project } from "../../../types";

interface UserProjectScopeProps {
  projects: Project[];
  selectedProjects: Set<string>;
  onToggleProject: (id: string) => void;
}

export function UserProjectScope(props: UserProjectScopeProps) {
  return (
    <section class="bg-surface-container-low border-outline-variant/15 space-y-6 rounded-xl border p-8 shadow-sm">
      <div class="flex items-center gap-3">
        <div class="bg-primary/10 border-primary/20 text-primary flex h-7 w-7 shrink-0 items-center justify-center rounded-full border font-mono text-xs font-bold shadow-[0_0_12px_rgba(99,102,241,0.15)]">
          03
        </div>
        <h3 class="font-headline text-on-surface text-lg font-bold">Project Scope</h3>
      </div>
      <div class="bg-surface-container-low border-outline-variant/15 overflow-hidden rounded-xl border">
        <div class="bg-surface-container-low border-outline-variant/15 text-outline grid grid-cols-2 border-b px-6 py-3.5 text-[10px] font-bold tracking-widest uppercase">
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
              const isGiven = () => props.selectedProjects.has(projectName);
              return (
                <div
                  data-testid={`invite-project-row-${project.urlSlug}`}
                  class="hover:bg-surface-container-high/40 border-outline-variant/10 grid cursor-pointer grid-cols-2 items-center border-b px-6 py-4 transition-colors last:border-b-0"
                  onClick={() => props.onToggleProject(projectName)}
                >
                  <div class="flex items-center gap-3">
                    <input
                      data-testid={`invite-project-${project.urlSlug}`}
                      type="checkbox"
                      checked={isGiven()}
                      onChange={() => props.onToggleProject(projectName)}
                      onClick={e => e.stopPropagation()}
                      aria-label={`Toggle access for project ${project.name || project.urlSlug}`}
                      class="bg-surface-container-low border-outline-variant/20 focus:ring-primary/30 text-primary accent-primary h-4 w-4 cursor-pointer rounded border focus:ring-1"
                    />
                    <span class="text-on-surface font-mono text-sm font-semibold">
                      {project.urlSlug}
                    </span>
                  </div>
                  <div class="text-right">
                    <Show
                      when={isGiven()}
                      fallback={
                        <span class="bg-surface-container-high text-outline border-outline-variant/15 rounded-full border px-2.5 py-0.5 text-[9px] font-bold tracking-wider uppercase">
                          None
                        </span>
                      }
                    >
                      <span class="bg-primary/10 text-primary border-primary/20 rounded-full border px-2.5 py-0.5 text-[9px] font-bold tracking-wider uppercase">
                        Inherited
                      </span>
                    </Show>
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
