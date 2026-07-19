import { For, Show, createMemo, createSignal } from "solid-js";
import { Button } from "../../shared/ui/button";
import { Input } from "../../shared/ui/input";
import { Label } from "../../shared/ui/label";
import { MIcon } from "../../shared/ui/icons";
import type { ConfigRelease } from "../../types";

interface ProjectReleasesProps {
  environmentName: string;
  activeReleaseVersion?: string | null;
  releases: ConfigRelease[];
  isLoading: boolean;
  isPublishing: boolean;
  isActivating: boolean;
  draftingVersion: string | null;
  deletingVersion: string | null;
  canManage: boolean;
  onPublish: (version: string, makeActive: boolean) => Promise<unknown>;
  onActivate: (version: string) => void;
  onClearActive: () => void;
  onDraft: (version: string) => void;
  onDelete: (version: string) => void;
}

const exactVersionPattern = /^\d+\.\d+\.\d+$/;

export function ProjectReleases(props: ProjectReleasesProps) {
  const [version, setVersion] = createSignal("");
  const [makeActive, setMakeActive] = createSignal(true);
  const [error, setError] = createSignal("");

  const activeRelease = createMemo(() =>
    props.releases.find(release => release.version === props.activeReleaseVersion)
  );

  const handlePublish = async (event: Event) => {
    event.preventDefault();
    const trimmed = version().trim();
    if (!exactVersionPattern.test(trimmed)) {
      setError("Use major.minor.patch.");
      return;
    }

    setError("");
    try {
      await props.onPublish(trimmed, makeActive());
      setVersion("");
    } catch {
      return;
    }
  };

  return (
    <section
      id="releases"
      data-testid="project-releases-section"
      class="bg-surface-container-low border-outline-variant/15 space-y-4 rounded-2xl border p-5 scroll-mt-20"
    >
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
            Publish and activate releases for the active environment: {props.environmentName}.
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
        <form
          onSubmit={handlePublish}
          class="bg-surface-container border-outline-variant/15 grid gap-3 rounded-2xl border p-4 shadow-sm md:grid-cols-[minmax(180px,1fr)_auto]"
        >
          <div class="space-y-2">
            <div>
              <Label for="release-version-input">Version</Label>
              <Input
                data-testid="release-version-input"
                id="release-version-input"
                aria-label="Release version"
                value={version()}
                onInput={event => {
                  setVersion(event.currentTarget.value);
                  if (error()) setError("");
                }}
                placeholder="1.1.0"
                class="font-mono"
              />
            </div>
            <label class="text-on-surface-variant flex cursor-pointer items-center gap-2 text-[12px]">
              <input
                type="checkbox"
                checked={makeActive()}
                onChange={event => setMakeActive(event.currentTarget.checked)}
                class="accent-primary"
              />
              Set active after publish
            </label>
            <Show when={error()}>
              <p class="text-error text-[11px] font-bold">{error()}</p>
            </Show>
          </div>
          <div class="flex items-center justify-end">
            <Button
              data-testid="release-publish-button"
              type="submit"
              disabled={props.isPublishing || !props.environmentName}
            >
              <MIcon name="add" class="text-[16px]" />
              {props.isPublishing ? "Publishing" : "Create"}
            </Button>
          </div>
        </form>
      </Show>

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

                  <Show when={props.canManage}>
                    <div class="flex flex-wrap items-center justify-end gap-2">
                      <button
                        type="button"
                        onClick={() => props.onActivate(release.version)}
                        disabled={props.isActivating || release.isActive}
                        class="bg-surface-container-high text-on-surface hover:bg-surface-bright inline-flex h-9 cursor-pointer items-center gap-1.5 rounded-lg border-0 px-3 text-[12px] font-semibold disabled:cursor-default disabled:opacity-50"
                      >
                        <MIcon name="check_circle" class="text-[15px]" />
                        Activate
                      </button>
                      <button
                        data-testid={`release-draft-${release.version}`}
                        type="button"
                        onClick={() => props.onDraft(release.version)}
                        disabled={props.draftingVersion !== null}
                        class="bg-surface-container-high text-on-surface hover:bg-surface-bright inline-flex h-9 cursor-pointer items-center gap-1.5 rounded-lg border-0 px-3 text-[12px] font-semibold disabled:opacity-50"
                      >
                        <MIcon name="edit_document" class="text-[15px]" />
                        {props.draftingVersion === release.version ? "Drafting" : "Draft"}
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
                        class="bg-error-container/10 text-error hover:bg-error-container/20 inline-flex h-9 cursor-pointer items-center gap-1.5 rounded-lg border-0 px-3 text-[12px] font-semibold disabled:cursor-default disabled:opacity-50"
                      >
                        <MIcon name="delete" class="text-[15px]" />
                        {props.deletingVersion === release.version ? "Deleting" : "Delete"}
                      </button>
                    </div>
                  </Show>
                </div>
              )}
            </For>
          </div>
        </Show>
      </Show>
    </section>
  );
}
