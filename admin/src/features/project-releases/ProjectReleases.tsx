import { For, Show, createMemo, createSignal } from "solid-js";
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
    <section class="border-outline-variant/15 bg-surface-container-low rounded-2xl border p-5">
      <div class="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
        <div class="min-w-0">
          <div class="flex items-center gap-2">
            <MIcon name="deployed_code_history" class="text-primary text-[20px]" />
            <h2 class="text-on-surface text-sm font-bold">Releases</h2>
          </div>
          <div class="mt-2 flex flex-wrap items-center gap-2 text-[12px]">
            <span class="text-on-surface-variant">Active</span>
            <Show
              when={activeRelease()}
              fallback={<span class="text-outline font-mono">none</span>}
            >
              {release => (
                <span class="bg-primary/10 text-primary rounded-md px-2 py-1 font-mono font-bold">
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
          <form onSubmit={handlePublish} class="flex w-full flex-col gap-2 sm:w-auto">
            <div class="flex flex-col gap-2 sm:flex-row">
              <input
                data-testid="release-version-input"
                aria-label="Release version"
                value={version()}
                onInput={event => {
                  setVersion(event.currentTarget.value);
                  if (error()) setError("");
                }}
                placeholder="1.1.0"
                class="border-outline-variant/20 bg-surface text-on-surface h-10 rounded-lg border px-3 font-mono text-[13px] outline-none transition-colors focus:border-primary"
              />
              <button
                data-testid="release-publish-button"
                type="submit"
                disabled={props.isPublishing || !props.environmentName}
                class="bg-primary text-on-primary inline-flex h-10 cursor-pointer items-center justify-center gap-2 rounded-lg border-0 px-4 text-[13px] font-semibold transition-all hover:brightness-105 disabled:opacity-50"
              >
                <MIcon name="publish" class="text-[16px]" />
                {props.isPublishing ? "Publishing" : "Publish"}
              </button>
            </div>
            <label class="text-on-surface-variant flex cursor-pointer items-center gap-2 text-[12px]">
              <input
                type="checkbox"
                checked={makeActive()}
                onChange={event => setMakeActive(event.currentTarget.checked)}
                class="accent-primary"
              />
              Set active
            </label>
            <Show when={error()}>
              <p class="text-error text-[11px] font-bold">{error()}</p>
            </Show>
          </form>
        </Show>
      </div>

      <div class="mt-4">
        <Show
          when={!props.isLoading}
          fallback={<p class="text-on-surface-variant text-sm">Loading releases</p>}
        >
          <Show
            when={props.releases.length > 0}
            fallback={<p class="text-on-surface-variant text-sm">No releases</p>}
          >
            <div class="divide-outline-variant/10 divide-y">
              <For each={props.releases}>
                {release => (
                  <div class="flex flex-col gap-3 py-3 sm:flex-row sm:items-center sm:justify-between">
                    <div class="min-w-0">
                      <div class="flex flex-wrap items-center gap-2">
                        <span class="text-on-surface font-mono text-[13px] font-bold">
                          {release.version}
                        </span>
                        <Show when={release.isActive}>
                          <span class="bg-primary/10 text-primary rounded-md px-2 py-0.5 text-[11px] font-bold">
                            Active
                          </span>
                        </Show>
                      </div>
                      <p class="text-on-surface-variant mt-1 text-[12px]">
                        {release.entryCount} parameters - {release.actor}
                      </p>
                    </div>
                    <Show when={props.canManage}>
                      <div class="flex flex-wrap gap-2">
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
      </div>
    </section>
  );
}
