import { Show, type ParentProps } from "solid-js";

import { MIcon } from "../../shared/ui/icons";
import type { ConfigRelease } from "../../types";
import { Tooltip, TooltipTrigger } from "../../shared/ui/tooltip";
import { tooltipCopy } from "../../shared/lib/tooltip-copy";

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

interface ReleaseActionTooltipProps extends ParentProps {
  content: string;
  disabled: boolean;
  disabledLabel: string;
}

function ReleaseActionTooltip(props: ReleaseActionTooltipProps) {
  return (
    <Tooltip content={props.content}>
      <TooltipTrigger
        as="span"
        tabindex={props.disabled ? "0" : undefined}
        role={props.disabled ? "group" : undefined}
        aria-label={props.disabled ? props.disabledLabel : undefined}
        data-tooltip-trigger
        class="inline-flex rounded-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
      >
        {props.children}
      </TooltipTrigger>
    </Tooltip>
  );
}

export function ReleaseRow(props: ReleaseRowProps) {
  const activateDisabled = () => props.isActivating || props.release.isActive;
  const activateTooltip = () => {
    if (props.release.isActive) return "This release is already active.";
    if (props.isActivating) return "A release activation is already in progress.";
    return `Make ${props.release.version} the default for unversioned clients.`;
  };
  const amendDisabled = () => props.amendingVersion !== null;
  const amendTooltip = () => {
    if (props.amendingVersion === props.release.version) {
      return `Release ${props.release.version} is being amended.`;
    }
    if (props.amendingVersion) {
      return `Finish amending ${props.amendingVersion} before starting another amendment.`;
    }
    return tooltipCopy.amend;
  };
  const deleteDisabled = () => props.release.isActive || props.deletingVersion !== null;
  const deleteTooltip = () => {
    if (props.release.isActive) return "Clear the active release before deleting it.";
    if (props.deletingVersion === props.release.version) {
      return `Release ${props.release.version} is being deleted.`;
    }
    if (props.deletingVersion) {
      return `Wait for release ${props.deletingVersion} to finish deleting.`;
    }
    return `Delete release ${props.release.version}.`;
  };

  return (
    <div class="bg-surface-container grid gap-3 rounded-xl px-4 py-3 md:grid-cols-[minmax(200px,1fr)_auto] md:items-center">
      <div class="min-w-0">
        <div class="flex flex-wrap items-center gap-2">
          <span class="text-on-surface truncate font-mono text-[14px] font-bold">
            {props.release.version}
          </span>
          <Show when={props.release.isActive}>
            <Tooltip content={tooltipCopy.activeRelease}><TooltipTrigger as="span" tabindex="0" data-tooltip-trigger class="bg-primary/10 text-primary rounded-md px-2 py-0.5 text-[12px] font-bold">Active</TooltipTrigger></Tooltip>
          </Show>
        </div>
        <p class="text-on-surface-variant mt-1 text-[13px]">
          {props.release.entryCount} parameters
        </p>
        <p class="text-outline mt-0.5 text-[12px]">Published by {props.release.actor}</p>
      </div>

      <div class="flex flex-wrap items-center justify-end gap-2">
        <Tooltip content={`View the immutable parameters in release ${props.release.version}.`}><TooltipTrigger as="button"
          data-testid={`release-view-${props.release.version}`}
          type="button"
          onClick={() => props.onView(props.release.version)}
          aria-label={`View parameters for release ${props.release.version}`}
          data-tooltip-trigger
          class="bg-surface-container-high text-on-surface hover:bg-surface-bright inline-flex h-9 w-9 cursor-pointer items-center justify-center gap-1.5 rounded-lg border-0 px-0 text-[13px] font-semibold disabled:cursor-default disabled:opacity-50 md:w-auto md:px-3"
        >
          <MIcon name="visibility" class="text-[15px]" />
          <span class="hidden md:inline">View parameters</span>
        </TooltipTrigger></Tooltip>
        <Show when={props.canManage}>
          <ReleaseActionTooltip
            content={activateTooltip()}
            disabled={activateDisabled()}
            disabledLabel={`Activate release ${props.release.version} unavailable`}
          >
            <button
              type="button"
              onClick={() => props.onActivate(props.release.version)}
              disabled={activateDisabled()}
              aria-label={`Activate release ${props.release.version}`}
              class="bg-surface-container-high text-on-surface hover:bg-surface-bright inline-flex h-9 w-9 cursor-pointer items-center justify-center gap-1.5 rounded-lg border-0 px-0 text-[13px] font-semibold disabled:cursor-default disabled:opacity-50 md:w-auto md:px-3"
            >
              <MIcon name="check_circle" class="text-[15px]" />
              <span class="hidden md:inline">Activate</span>
            </button>
          </ReleaseActionTooltip>
          <ReleaseActionTooltip
            content={amendTooltip()}
            disabled={amendDisabled()}
            disabledLabel={`Amend release ${props.release.version} unavailable`}
          >
            <button
              data-testid={`release-amend-${props.release.version}`}
              type="button"
              onClick={() => props.onAmend(props.release.version)}
              disabled={amendDisabled()}
              aria-label={`Amend release ${props.release.version}`}
              class="bg-surface-container-high text-on-surface hover:bg-surface-bright inline-flex h-9 w-9 cursor-pointer items-center justify-center gap-1.5 rounded-lg border-0 px-0 text-[13px] font-semibold md:w-auto md:px-3"
            >
              <MIcon name="edit" class="text-[15px]" />
              <span class="hidden md:inline">
                {props.amendingVersion === props.release.version ? "Amending" : "Amend"}
              </span>
            </button>
          </ReleaseActionTooltip>
          <ReleaseActionTooltip
            content={deleteTooltip()}
            disabled={deleteDisabled()}
            disabledLabel={`Delete release ${props.release.version} unavailable`}
          >
            <button
              data-testid={`release-delete-${props.release.version}`}
              type="button"
              onClick={() => props.onDelete(props.release.version)}
              disabled={deleteDisabled()}
              aria-label={`Delete release ${props.release.version}`}
              class="bg-error-container/10 text-error hover:bg-error-container/20 inline-flex h-9 w-9 cursor-pointer items-center justify-center gap-1.5 rounded-lg border-0 px-0 text-[13px] font-semibold disabled:cursor-default disabled:opacity-50 md:w-auto md:px-3"
            >
              <MIcon name="delete" class="text-[15px]" />
              <span class="hidden md:inline">
                {props.deletingVersion === props.release.version ? "Deleting" : "Delete"}
              </span>
            </button>
          </ReleaseActionTooltip>
        </Show>
      </div>
    </div>
  );
}
