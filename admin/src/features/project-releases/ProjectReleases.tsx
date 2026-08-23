import { Show, createMemo } from "solid-js";

import { MIcon } from "../../shared/ui/icons";
import type { ConfigRelease } from "../../types";
import { ReleaseList } from "./ReleaseList";

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
            class="text-outline font-headline flex items-center gap-1.5 text-[11px] font-bold tracking-widest uppercase"
          >
            <MIcon name="deployed_code_history" class="text-[15px]" />
            Releases
          </p>
          <div class="mt-1 flex flex-wrap items-center gap-2 text-[13px]">
            <span class="text-on-surface-variant">
              Publish immutable snapshots for {props.environmentName}, then activate one for clients.
            </span>
            <span class="text-on-surface-variant">Active release:</span>
            <Show
              when={activeRelease()}
              fallback={<span class="text-outline font-mono">none</span>}
            >
              {release => (
                <span class="bg-primary/10 text-primary rounded-md px-2 py-0.5 font-mono text-[12px] font-bold">
                  {release().version}
                </span>
              )}
            </Show>
            <Show when={props.canManage && props.activeReleaseVersion}>
              <button
                type="button"
                onClick={() => props.onClearActive()}
                disabled={props.isActivating}
                class="text-on-surface-variant hover:text-on-surface cursor-pointer border-0 bg-transparent p-0 text-[13px] font-semibold disabled:opacity-50"
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
            class="bg-primary text-on-primary inline-flex h-10 w-10 shrink-0 cursor-pointer items-center justify-center gap-1.5 self-end rounded-lg border-0 px-0 text-[14px] font-semibold transition-all hover:brightness-105 active:scale-[0.98] disabled:opacity-50 md:h-10 md:w-auto md:px-4 md:self-auto"
          >
            <MIcon name="add" class="text-[17px]" />
            <span class="hidden md:inline">Create a version</span>
          </button>
        </Show>
      </div>

      <ReleaseList
        releases={props.releases}
        isLoading={props.isLoading}
        canManage={props.canManage}
        isActivating={props.isActivating}
        amendingVersion={props.amendingVersion}
        deletingVersion={props.deletingVersion}
        onView={props.onView}
        onAmend={props.onAmend}
        onActivate={props.onActivate}
        onDelete={props.onDelete}
      />
    </section>
  );
}
