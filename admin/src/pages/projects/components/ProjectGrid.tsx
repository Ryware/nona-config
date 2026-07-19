import { For, Show } from "solid-js";
import { MIcon } from "../../../shared/ui/icons";
import type { Project } from "../../../types";

interface ProjectGridProps {
  isLoading: boolean;
  isSuccess: boolean;
  projects: Project[];
  filteredProjects: Project[];
  search: string;
  onNavigate: (slug: string) => void;
  onDeleteTarget: (project: Project) => void;
  onCreateClick: () => void;
  canCreateProjects: boolean;
  canDeleteProjects: boolean;
}

export function ProjectGrid(props: ProjectGridProps) {
  return (
    <Show
      when={!props.isLoading}
      fallback={
        <div class="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
          <For each={[1, 2, 3, 4]}>
            {() => (
              <div class="bg-surface-container-low border-outline-variant/15 space-y-3 rounded-2xl border p-5">
                <div class="skeleton h-3 w-24 rounded" />
                <div class="skeleton h-4 w-2/3 rounded-lg" />
                <div class="skeleton h-3 w-full rounded" />
                <div class="skeleton h-3 w-4/5 rounded" />
                <div class="border-outline-variant/10 flex justify-between border-t pt-2">
                  <div class="skeleton h-3 w-16 rounded" />
                  <div class="skeleton h-5 w-5 rounded-md" />
                </div>
              </div>
            )}
          </For>
        </div>
      }
    >
      <Show
        when={props.isSuccess && props.projects.length > 0}
        fallback={
          <Show when={props.isSuccess}>
            <div class="bg-surface-container-low border-outline-variant/15 animate-fade-in rounded-2xl border p-16 text-center">
              <div class="bg-primary/5 mb-5 inline-flex h-16 w-16 items-center justify-center rounded-2xl">
                <MIcon name="folder_open" class="text-primary/60 text-4xl" />
              </div>
              <p class="text-on-surface font-headline mb-1 text-[14px] font-bold">
                No projects yet
              </p>
              <p class="text-on-surface-variant mb-6 text-[13px]">
                {props.canCreateProjects
                  ? "Create your first project to start managing configuration."
                  : "No projects are available for your account."}
              </p>
              <Show when={props.canCreateProjects}>
                <button
                  data-testid="empty-projects-new-button"
                  onClick={() => props.onCreateClick()}
                  aria-label="New Project"
                  title="New Project"
                  class="bg-primary text-on-primary inline-flex h-10 w-10 cursor-pointer items-center justify-center gap-1.5 rounded-lg border-0 px-0 text-[13px] font-semibold transition-all hover:brightness-105 md:h-auto md:w-auto md:px-4 md:py-2"
                >
                  <MIcon name="add" class="text-[17px]" />
                  <span class="hidden md:inline">New Project</span>
                </button>
              </Show>
            </div>
          </Show>
        }
      >
        <Show
          when={props.filteredProjects.length > 0}
          fallback={
            <div class="bg-surface-container-low border-outline-variant/15 rounded-2xl border p-10 text-center">
              <MIcon name="search_off" class="text-outline mb-3 block text-4xl" />
              <p class="text-on-surface-variant text-[13px]">
                No projects match "<span class="text-on-surface font-medium">{props.search}</span>"
              </p>
            </div>
          }
        >
          <div class="animate-stagger grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
            <For each={props.filteredProjects}>
              {project => (
                <div
                  data-testid={`project-card-${project.urlSlug}`}
                  role="link"
                  tabindex="0"
                  aria-label={`Open project ${project.name}`}
                  class="group bg-surface-container-low hover:bg-surface-container border-outline-variant/45 hover:border-outline-variant/30 focus-visible:ring-primary focus-visible:ring-offset-background cursor-pointer rounded-2xl border transition-all duration-200 hover:-translate-y-0.5 focus-visible:ring-2 focus-visible:ring-offset-2 focus-visible:outline-none"
                  onClick={() => props.onNavigate(project.urlSlug)}
                  onKeyDown={e => {
                    if (e.key === "Enter" || e.key === " ") {
                      e.preventDefault();
                      props.onNavigate(project.urlSlug);
                    }
                  }}
                >
                  <div class="flex h-full flex-col p-5">
                    <span class="text-outline mb-2 block truncate font-mono text-[11px]">
                      {project.urlSlug}
                    </span>

                    <h3
                      class="font-headline text-on-surface mb-2 truncate text-[14px] leading-tight font-semibold"
                      title={project.name}
                    >
                      {project.name}
                    </h3>

                    <p class="text-on-surface-variant mb-4 line-clamp-2 flex-1 text-[12.5px] leading-relaxed">
                      {project.description || "No description provided."}
                    </p>

                    <div class="border-outline-variant/10 flex items-center justify-between border-t pt-3">
                      <span class="text-outline text-[11.5px]">
                        {new Date(project.updatedAt).toLocaleDateString("en-US", {
                          month: "short",
                          day: "numeric"
                        })}
                      </span>
                      <div class="flex items-center gap-1">
                        <Show when={props.canDeleteProjects}>
                          <button
                            data-testid={`project-delete-${project.urlSlug}`}
                            onClick={e => {
                              e.stopPropagation();
                              props.onDeleteTarget(project);
                            }}
                            class="text-outline hover:text-error hover:bg-error/10 cursor-pointer rounded-lg border-0 bg-transparent p-1.5 opacity-40 transition-colors group-hover:opacity-100 focus:opacity-100"
                            title={`Delete ${project.name}`}
                          >
                            <MIcon name="delete_outline" class="text-[16px]" />
                          </button>
                        </Show>
                        <div class="text-outline/50 group-hover:text-primary flex h-6 w-6 items-center justify-center rounded-md transition-colors">
                          <MIcon name="arrow_forward" class="text-[15px]" />
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              )}
            </For>

            <Show when={props.canCreateProjects}>
              <button
                data-testid="project-grid-new-button"
                onClick={() => props.onCreateClick()}
                class="border-outline-variant/40 text-outline hover:border-primary/30 hover:text-primary hover:bg-primary/5 flex min-h-37 cursor-pointer flex-col items-center justify-center gap-2 rounded-2xl border border-dashed bg-transparent p-5 transition-all"
                aria-label="New Project"
                title="New Project"
              >
                <MIcon name="add" class="text-2xl" />
                <span class="hidden md:inline text-[12.5px] font-medium">New Project</span>
              </button>
            </Show>
          </div>
        </Show>
      </Show>
    </Show>
  );
}
