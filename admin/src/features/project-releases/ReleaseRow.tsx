import { Show } from "solid-js";

import { MIcon } from "../../shared/ui/icons";
import type { ConfigRelease } from "../../types";

export interface ReleaseRowActions {
  canManage: boolean;
  isActivating: boolean;
  amendingVersion: string | null;
  deletingVersion: string | null;
  onView: (version: string) => void;
  onAmend: (version: string) => void;
  onActivate: (version: string) => void;
  onDelete: (version: string) => void;
}

interface ReleaseRowProps extends ReleaseRowActions {
  release: ConfigRelease;
}

export function ReleaseRow(props: ReleaseRowProps) {
  return (
    <div class="bg-surface-container grid gap-3 rounded-xl px-4 py-3 md:grid-cols-[minmax(200px,1fr)_auto] md:items-center">
      <div class="min-w-0">
        <div class="flex flex-wrap items-center gap-2">
          <span class="text-on-surface truncate font-mono text-[13px] font-bold">
            {props.release.version}
          </span>
          <Show when={props.release.isActive}>
            <span class="bg-primary/10 text-primary rounded-md px-2 py-0.5 text-[11px] font-bold">
              Active
            </span>
          </Show>
        </div>
        <p class="text-on-surface-variant mt-1 text-[12px]">
          {props.release.entryCount} parameters
        </p>
        <p class="text-outline mt-0.5 text-[11px]">Published by {props.release.actor}</p>
      </div>

      <div class="flex flex-wrap items-center justify-end gap-2">
        <button
          data-testid={`release-view-${props.release.version}`}
          type="button"
          onClick={() => props.onView(props.release.version)}
          aria-label={`View parameters for release ${props.release.version}`}
          title={`View parameters for release ${props.release.version}`}
          class="bg-surface-container-high text-on-surface hover:bg-surface-bright inline-flex h-9 w-9 cursor-pointer items-center justify-center gap-1.5 rounded-lg border-0 px-0 text-[12px] font-semibold disabled:cursor-default disabled:opacity-50 md:w-auto md:px-3"
        >
          <MIcon name="visibility" class="text-[15px]" />
          <span class="hidden md:inline">View parameters</span>
        </button>
        <Show when={props.canManage}>
          <button
            type="button"
            onClick={() => props.onActivate(props.release.version)}
            disabled={props.isActivating || props.release.isActive}
            aria-label={`Activate release ${props.release.version}`}
            title={`Activate release ${props.release.version}`}
            class="bg-surface-container-high text-on-surface hover:bg-surface-bright inline-flex h-9 w-9 cursor-pointer items-center justify-center gap-1.5 rounded-lg border-0 px-0 text-[12px] font-semibold disabled:cursor-default disabled:opacity-50 md:w-auto md:px-3"
          >
            <MIcon name="check_circle" class="text-[15px]" />
            <span class="hidden md:inline">Activate</span>
          </button>
          <button
            data-testid={`release-amend-${props.release.version}`}
            type="button"
            onClick={() => props.onAmend(props.release.version)}
            disabled={props.amendingVersion !== null}
            aria-label={`Amend release ${props.release.version}`}
            class="bg-surface-container-high text-on-surface hover:bg-surface-bright inline-flex h-9 w-9 cursor-pointer items-center justify-center gap-1.5 rounded-lg border-0 px-0 text-[12px] font-semibold md:w-auto md:px-3"
            title={`Amend release ${props.release.version} as a new patch`}
          >
            <MIcon name="edit" class="text-[15px]" />
            <span class="hidden md:inline">
              {props.amendingVersion === props.release.version ? "Amending" : "Amend"}
            </span>
          </button>
          <button
            data-testid={`release-delete-${props.release.version}`}
            type="button"
            onClick={() => props.onDelete(props.release.version)}
            disabled={props.release.isActive || props.deletingVersion !== null}
            title={
              props.release.isActive
                ? "Clear the active release before deleting it"
                : `Delete release ${props.release.version}`
            }
            aria-label={`Delete release ${props.release.version}`}
            class="bg-error-container/10 text-error hover:bg-error-container/20 inline-flex h-9 w-9 cursor-pointer items-center justify-center gap-1.5 rounded-lg border-0 px-0 text-[12px] font-semibold disabled:cursor-default disabled:opacity-50 md:w-auto md:px-3"
          >
            <MIcon name="delete" class="text-[15px]" />
            <span class="hidden md:inline">
              {props.deletingVersion === props.release.version ? "Deleting" : "Delete"}
            </span>
          </button>
        </Show>
      </div>
    </div>
  );
}
