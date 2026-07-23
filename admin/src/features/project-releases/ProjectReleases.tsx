import { For, Show, createMemo } from "solid-js";
import { MIcon } from "../../shared/ui/icons";
import type { ConfigRelease } from "../../types";

interface ProjectReleasesProps {
  environmentName: string;
  activeReleaseVersion?: string | null;
  releases: ConfigRelease[];
  isLoading: boolean;
  isActivating: boolean;
  amendingVersion: string | null;
  deletingVersion: string | null;
  canManage: boolean;
  onCreateVersion: () => void;
  onView: (version: string) => void;
  onAmend: (version: string) => void;
  onActivate: (version: string) => void;
  onClearActive: () => void;
  onDelete: (version: string) => void;
}

export function ProjectReleases(props: ProjectReleasesProps) {
  const activeRelease = createMemo(() =>
    props.releases.find(release => release.version === props.activeReleaseVersion)
  );

  return (
    <section
      id="releases"
      data-testid="project-releases-section"
      class="bg-surface-container-low border-outline-variant/15 space-y-4 rounded-2xl border p-5 scroll-mt-20"
    >
      <div class="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <p
            data-testid="project-releases-heading"
            class="text-outline font-headline flex items-center gap-1.5 text-[10px] font-bold tracking-widest uppercase"
          >
            <MIcon name="deployed_code_history" class="text-[15px]" />
            Releases
          </p>
          <div class="mt-1 flex flex-wrap items-center gap-2 text-xs">
            <span class="text-on-surface-variant">
              Publish immutable snapshots for {props.environmentName}, then activate one for clients.
            </span>
            <span class="text-on-surface-variant">Active release:</span>
            <Show
              when={activeRelease()}
              fallback={<span class="text-outline font-mono">none</span>}
            >
              {release => (
                <span class="bg-primary/10 text-primary rounded-md px-2 py-0.5 font-mono text-[11px] font-bold">
                  {release().version}
                </span>
              )}
            </Show>
            <Show when={props.canManage && props.activeReleaseVersion}>
              <button
                type="button"
                onClick={() => props.onClearActive()}
                disabled={props.isActivating}
                class="text-on-surface-variant hover:text-on-surface cursor-pointer border-0 bg-transparent p-0 text-[12px] font-semibold disabled:opacity-50"
              >
                Clear
              </button>
            </Show>
          </div>
        </div>

        <Show when={props.canManage}>
          <button
            data-testid="release-create-version-button"
            type="button"
            onClick={() => props.onCreateVersion()}
            disabled={!props.environmentName}
            aria-label="Create a version"
            title="Create a version"
            class="bg-primary text-on-primary inline-flex h-10 w-10 shrink-0 cursor-pointer items-center justify-center gap-1.5 self-end rounded-lg border-0 px-0 text-[13px] font-semibold transition-all hover:brightness-105 active:scale-[0.98] disabled:opacity-50 md:h-10 md:w-auto md:px-4 md:self-auto"
          >
            <MIcon name="add" class="text-[17px]" />
            <span class="hidden md:inline">Create a version</span>
          </button>
        </Show>
      </div>

      <Show
        when={!props.isLoading}
        fallback={<div class="skeleton h-20 w-full rounded-xl" />}
      >
        <Show
          when={props.releases.length > 0}
          fallback={
            <div class="bg-surface-container rounded-xl px-4 py-5 text-center text-xs text-on-surface-variant">
              No releases yet.
            </div>
          }
        >
          <div class="space-y-2">
            <For each={props.releases}>
              {release => (
                <div class="bg-surface-container grid gap-3 rounded-xl px-4 py-3 md:grid-cols-[minmax(200px,1fr)_auto] md:items-center">
                  <div class="min-w-0">
                    <div class="flex flex-wrap items-center gap-2">
                      <span class="text-on-surface truncate font-mono text-[13px] font-bold">
                        {release.version}
                      </span>
                      <Show when={release.isActive}>
                        <span class="bg-primary/10 text-primary rounded-md px-2 py-0.5 text-[11px] font-bold">
                          Active
                        </span>
                      </Show>
                    </div>
                    <p class="text-on-surface-variant mt-1 text-[12px]">
                      {release.entryCount} parameters
                    </p>
                    <p class="text-outline mt-0.5 text-[11px]">Published by {release.actor}</p>
                  </div>

                  <div class="flex flex-wrap items-center justify-end gap-2">
                    <button
                      data-testid={`release-view-${release.version}`}
                      type="button"
                      onClick={() => props.onView(release.version)}
                      aria-label={`View parameters for release ${release.version}`}
                      title={`View parameters for release ${release.version}`}
                      class="bg-surface-container-high text-on-surface hover:bg-surface-bright inline-flex h-9 w-9 cursor-pointer items-center justify-center gap-1.5 rounded-lg border-0 px-0 text-[12px] font-semibold disabled:cursor-default disabled:opacity-50 md:w-auto md:px-3"
                    >
                      <MIcon name="visibility" class="text-[15px]" />
                      <span class="hidden md:inline">View parameters</span>
                    </button>
                    <Show when={props.canManage}>
                      <button
                        type="button"
                        onClick={() => props.onActivate(release.version)}
                        disabled={props.isActivating || release.isActive}
                        aria-label={`Activate release ${release.version}`}
                        title={`Activate release ${release.version}`}
                        class="bg-surface-container-high text-on-surface hover:bg-surface-bright inline-flex h-9 w-9 cursor-pointer items-center justify-center gap-1.5 rounded-lg border-0 px-0 text-[12px] font-semibold disabled:cursor-default disabled:opacity-50 md:w-auto md:px-3"
                      >
                        <MIcon name="check_circle" class="text-[15px]" />
                        <span class="hidden md:inline">Activate</span>
                      </button>
                      <button
                        data-testid={`release-amend-${release.version}`}
                        type="button"
                        onClick={() => props.onAmend(release.version)}
                        disabled={props.amendingVersion !== null}
                        aria-label={`Amend release ${release.version}`}
                        class="bg-surface-container-high text-on-surface hover:bg-surface-bright inline-flex h-9 w-9 cursor-pointer items-center justify-center gap-1.5 rounded-lg border-0 px-0 text-[12px] font-semibold md:w-auto md:px-3"
                        title={`Amend release ${release.version} as a new patch`}
                      >
                        <MIcon name="edit" class="text-[15px]" />
                        <span class="hidden md:inline">
                          {props.amendingVersion === release.version ? "Amending" : "Amend"}
                        </span>
                      </button>
                      <button
                        data-testid={`release-delete-${release.version}`}
                        type="button"
                        onClick={() => props.onDelete(release.version)}
                        disabled={release.isActive || props.deletingVersion !== null}
                        title={
                          release.isActive
                            ? "Clear the active release before deleting it"
                            : `Delete release ${release.version}`
                        }
                        aria-label={`Delete release ${release.version}`}
                        class="bg-error-container/10 text-error hover:bg-error-container/20 inline-flex h-9 w-9 cursor-pointer items-center justify-center gap-1.5 rounded-lg border-0 px-0 text-[12px] font-semibold disabled:cursor-default disabled:opacity-50 md:w-auto md:px-3"
                      >
                        <MIcon name="delete" class="text-[15px]" />
                        <span class="hidden md:inline">
                          {props.deletingVersion === release.version ? "Deleting" : "Delete"}
                        </span>
                      </button>
                    </Show>
                  </div>
                </div>
              )}
            </For>
          </div>
        </Show>
      </Show>
    </section>
  );
}
