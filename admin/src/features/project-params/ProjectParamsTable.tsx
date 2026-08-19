import { createMediaQuery } from "@solid-primitives/media";
import { For, Show, createMemo } from "solid-js";
import { ProjectParamEditDrawer } from "../project-param-edit/ProjectParamEditDrawer";
import { MIcon } from "../../shared/ui/icons";
import { cn } from "../../shared/lib/utils";
import type { ConfigEntry, ConfigEntryVersion } from "../../types";
import { Tooltip, TooltipTrigger } from "../../shared/ui/tooltip";
import { tooltipCopy } from "../../shared/lib/tooltip-copy";

export type ParameterViewDensity = "comfortable" | "compact";

export interface ProjectParamsTableProps {
  isLoading: boolean;
  projectId: string;
  activeEnvName: string;
  filteredConfig: ConfigEntry[];
  editingEntry: ConfigEntry | null;
  onSelectEntry: (entry: ConfigEntry) => void;
  onShareEntry: (entry: ConfigEntry) => void;
  onDeleteEntry: (key: string) => void;
  canManage: boolean;
  copiedKey: string | null;
  onCopyValue: (key: string, value: string) => void;
  getParamMeta: (
    proj: string,
    env: string,
    key: string
  ) => { displayName: string; description: string };
  initialDescription: string;
  onCloseEntry: () => void;
  onEditDirtyChange: (dirty: boolean) => void;
  onSaveSettings: (data: {
    value: string;
    description: string;
    contentType: ConfigEntry["contentType"];
    scope: ConfigEntry["scope"];
  }) => void;
  isSaving: boolean;
  historyVersions: ConfigEntryVersion[];
  isHistoryLoading: boolean;
  isRollingBack: boolean;
  onRollbackVersion: (version: ConfigEntryVersion) => void;
  search: string;
  isReadOnly?: boolean;
  releaseVersion?: string;
  density?: ParameterViewDensity;
}

const TYPE_STYLE: Record<string, string> = {
  string: "bg-primary/10 border border-primary/20 text-primary",
  number: "bg-secondary/10 border border-secondary/20 text-secondary",
  boolean: "bg-amber-500/10 border border-amber-500/20 text-amber-400",
  json: "bg-purple-500/10 border border-purple-500/20 text-purple-400"
};

const SCOPE_STYLE: Record<string, string> = {
  all: "bg-surface-container-high/80 border border-outline-variant/15 text-outline",
  client: "bg-primary/10 border border-primary/20 text-primary",
  server: "bg-secondary/10 border border-secondary/20 text-secondary"
};

export function ProjectParamsTable(props: ProjectParamsTableProps) {
  const isMobile = createMediaQuery("(max-width: 767px)");
  const isCompact = () => props.density === "compact";

  return (
    <div
      data-testid="parameter-table"
      data-density={props.density ?? "comfortable"}
      class={isCompact() ? "space-y-1.5" : "space-y-3"}
    >
      <Show when={isMobile()}>
        <div class={isCompact() ? "space-y-1.5" : "space-y-3"}>
        <Show when={props.isLoading}>
          <For each={[1, 2, 3]}>
            {() => (
              <div
                class={cn(
                  "skeleton w-full",
                  isCompact() ? "h-24 rounded-xl" : "h-36 rounded-2xl"
                )}
              />
            )}
          </For>
        </Show>

        <Show when={!props.isLoading}>
          <For each={props.filteredConfig}>
            {entry => {
              const meta = createMemo(() =>
                props.getParamMeta(props.projectId, props.activeEnvName, entry.key)
              );
              const isExpanded = () => props.editingEntry?.key === entry.key;

              return (
                <article
                  class={cn(
                    "bg-surface-container border-outline-variant/10 overflow-hidden border",
                    isCompact() ? "rounded-xl" : "rounded-2xl"
                  )}
                >
                  <div
                    data-testid={`parameter-row-${entry.key}`}
                    role="button"
                    tabindex="0"
                    onClick={() => props.onSelectEntry(entry)}
                    onKeyDown={event => {
                      if (event.key === "Enter" || event.key === " ") {
                        event.preventDefault();
                        props.onSelectEntry(entry);
                      }
                    }}
                    class={cn(
                      "w-full cursor-pointer border-0 bg-transparent text-left",
                      isCompact() ? "p-3" : "p-4"
                    )}
                  >
                    <div class={cn("flex items-start", isCompact() ? "gap-2" : "gap-3")}>
                      <div
                        class={cn(
                          "bg-surface-container-high text-outline mt-0.5 flex shrink-0 items-center justify-center transition-transform",
                          isCompact() ? "h-6 w-6 rounded-md" : "h-7 w-7 rounded-lg",
                          isExpanded() && "rotate-180"
                        )}
                      >
                        <MIcon name="expand_more" class="text-[16px]" />
                      </div>

                      <div class={cn("min-w-0 flex-1", isCompact() ? "space-y-1.5" : "space-y-3")}>
                        <div class="min-w-0">
                          <span
                            data-testid={`parameter-display-${entry.key}`}
                            class="text-on-surface block text-[13.5px] font-bold"
                          >
                            {meta().displayName}
                          </span>
                          <span
                            data-testid={`parameter-key-${entry.key}`}
                            class={cn(
                              "text-outline block font-mono text-[10px] tracking-tight break-all",
                              !isCompact() && "mt-0.5"
                            )}
                          >
                            {entry.key}
                          </span>
                        </div>

                        <div class={cn("flex flex-wrap", isCompact() ? "gap-1.5" : "gap-2")}>
                          <span
                            class={cn(
                              "rounded-full text-[9px] font-bold tracking-wider uppercase",
                              isCompact() ? "px-1.5 py-0" : "px-2 py-0.5",
                              TYPE_STYLE[entry.contentType] ?? ""
                            )}
                          >
                            {entry.contentType}
                          </span>
                          <span
                            class={cn(
                              "rounded-full text-[9px] font-bold tracking-wider uppercase",
                              isCompact() ? "px-1.5 py-0" : "px-2 py-0.5",
                              SCOPE_STYLE[entry.scope] ?? ""
                            )}
                          >
                            {entry.scope}
                          </span>
                        </div>

                        <div
                          class={cn(
                            "bg-surface-container-lowest/60 flex items-center gap-2",
                            isCompact() ? "rounded-lg px-2 py-1" : "rounded-xl px-3 py-2"
                          )}
                          onClick={e => e.stopPropagation()}
                        >
                          <span
                            data-testid={`parameter-value-${entry.key}`}
                            class="text-on-surface-variant min-w-0 flex-1 truncate font-mono text-[12px]"
                          >
                            {entry.value}
                          </span>
                          <button
                            type="button"
                            onClick={() => void props.onCopyValue(entry.key, entry.value)}
                            title="Copy value"
                            class="text-outline hover:text-primary hover:bg-primary/10 flex shrink-0 cursor-pointer items-center justify-center rounded border-0 bg-transparent p-1"
                          >
                            <MIcon
                              name={props.copiedKey === entry.key ? "check" : "content_copy"}
                              class="text-[14px]"
                            />
                          </button>
                        </div>
                      </div>
                    </div>
                  </div>

                  <Show when={props.canManage}>
                    <div
                      class={cn(
                        "border-outline-variant/10 flex justify-end gap-1 border-t",
                        isCompact() ? "px-3 py-1" : "px-4 py-2"
                      )}
                    >
                      <button
                        data-testid={`parameter-share-${entry.key}`}
                        type="button"
                        onClick={() => props.onShareEntry(entry)}
                        class="text-outline hover:text-primary hover:bg-primary/10 cursor-pointer rounded-lg border-0 bg-transparent p-1.5"
                        title={`Share parameter ${entry.key}`}
                        aria-label={`Share parameter ${entry.key}`}
                      >
                        <MIcon name="ios_share" class="text-[18px]" />
                      </button>
                      <button
                        data-testid={`parameter-delete-${entry.key}`}
                        type="button"
                        onClick={() => props.onDeleteEntry(entry.key)}
                        class="text-outline hover:text-error hover:bg-error/10 cursor-pointer rounded-lg border-0 bg-transparent p-1.5"
                        title={`Delete parameter ${entry.key}`}
                        aria-label={`Delete parameter ${entry.key}`}
                      >
                        <MIcon name="delete_outline" class="text-[18px]" />
                      </button>
                    </div>
                  </Show>

                  <Show when={isExpanded()}>
                    <div
                      data-testid={`parameter-accordion-${entry.key}`}
                      class={cn(
                        "bg-surface-container-lowest/30 border-outline-variant/10 border-t",
                        isCompact() ? "px-3 py-2.5" : "px-4 py-4"
                      )}
                    >
                      <ProjectParamEditDrawer
                        {...props}
                        entry={props.editingEntry}
                        onClose={props.onCloseEntry}
                        onDirtyChange={props.onEditDirtyChange}
                        historyLayout="mobile"
                      />
                    </div>
                  </Show>
                </article>
              );
            }}
          </For>
        </Show>

        <Show when={!props.isLoading && props.search && props.filteredConfig.length === 0}>
          <div
            class={cn(
              "text-on-surface-variant text-center text-sm",
              isCompact() ? "py-6" : "py-10"
            )}
          >
            No parameters match "<span class="text-on-surface font-medium">{props.search}</span>"
          </div>
        </Show>
        </div>
      </Show>

      <Show when={!isMobile()}>
        <div
          class={cn(
            "bg-surface-container-low border-outline-variant/15 overflow-hidden border",
            isCompact() ? "rounded-lg" : "rounded-xl"
          )}
        >
        <div class="overflow-x-auto">
        <table
          data-testid="parameter-desktop-table"
          class="w-full min-w-[48rem] table-fixed border-collapse text-left text-[12px]"
        >
          <colgroup>
            <col class="w-[17rem]" />
            <col />
            <col class="w-28" />
            <col class="w-28" />
            <col class="w-24" />
          </colgroup>
          <thead class="sticky top-0 z-10">
            <tr class="border-outline-variant/15 bg-surface-container-lowest/50 border-b">
              <th
                class={cn(
                  "text-outline text-[11px] font-medium tracking-[0.05em] uppercase",
                  isCompact() ? "px-2.5 py-2" : "px-4 py-3"
                )}
              >
                Parameter
              </th>
              <th
                class={cn(
                  "text-outline text-[11px] font-medium tracking-[0.05em] uppercase",
                  isCompact() ? "px-2.5 py-2" : "px-4 py-3"
                )}
              >
                Value
              </th>
              <th
                class={cn(
                  "text-outline text-[11px] font-medium tracking-[0.05em] uppercase",
                  isCompact() ? "px-2.5 py-2" : "px-4 py-3"
                )}
              >
                <Tooltip content={tooltipCopy.datatype}><TooltipTrigger as="span" tabindex="0" data-tooltip-trigger class="cursor-help border-b border-dotted border-outline/60">Type</TooltipTrigger></Tooltip>
              </th>
              <th
                class={cn(
                  "text-outline text-[11px] font-medium tracking-[0.05em] uppercase",
                  isCompact() ? "px-2.5 py-2" : "px-4 py-3"
                )}
              >
                <Tooltip content={tooltipCopy.scope}><TooltipTrigger as="span" tabindex="0" data-tooltip-trigger class="cursor-help border-b border-dotted border-outline/60">Scope</TooltipTrigger></Tooltip>
              </th>
              <th
                class={cn(
                  "text-outline text-right text-[11px] font-medium tracking-[0.05em] uppercase",
                  isCompact() ? "px-2.5 py-2" : "px-4 py-3"
                )}
              >
                <Show when={!props.isReadOnly} fallback={<>Details</>}>
                  Actions
                </Show>
              </th>
            </tr>
          </thead>
          <tbody class="divide-outline-variant/10 animate-stagger divide-y">
            <Show when={props.isLoading}>
              <For each={[1, 2, 3]}>
                {() => (
                  <tr>
                    <td class={isCompact() ? "px-2.5 py-2" : "px-4 py-4"}>
                      <div class="skeleton h-4 w-40 rounded" />
                    </td>
                    <td class={isCompact() ? "px-2.5 py-2" : "px-4 py-4"}>
                      <div class="skeleton h-4 w-32 rounded" />
                    </td>
                    <td class={isCompact() ? "px-2.5 py-2" : "px-4 py-4"}>
                      <div class="skeleton h-5 w-14 rounded-full" />
                    </td>
                    <td class={isCompact() ? "px-2.5 py-2" : "px-4 py-4"}>
                      <div class="skeleton h-5 w-14 rounded-full" />
                    </td>
                    <td class={isCompact() ? "px-2.5 py-2" : "px-4 py-4"} />
                  </tr>
                )}
              </For>
            </Show>
            <Show when={!props.isLoading}>
              <For each={props.filteredConfig}>
                {entry => {
                  const meta = createMemo(() =>
                    props.getParamMeta(props.projectId, props.activeEnvName, entry.key)
                  );
                  const isExpanded = () => props.editingEntry?.key === entry.key;

                  return (
                    <>
                      <tr
                        data-testid={`parameter-row-${entry.key}`}
                        onClick={() => props.onSelectEntry(entry)}
                        class={cn(
                          "group cursor-pointer transition-colors",
                          isExpanded()
                            ? "bg-surface-container-high/40"
                            : "hover:bg-surface-container-high/40"
                        )}
                      >
                        <td
                          class={cn(
                            "min-w-0 overflow-hidden",
                            isCompact() ? "px-2.5 py-2" : "px-4 py-4"
                          )}
                        >
                          <div
                            class={cn(
                              "flex min-w-0 items-center",
                              isCompact() ? "gap-2" : "gap-3"
                            )}
                          >
                            <div
                              class={cn(
                                "bg-surface-container-high text-outline flex shrink-0 items-center justify-center transition-transform",
                                isCompact() ? "h-6 w-6 rounded-md" : "h-7 w-7 rounded-lg",
                                isExpanded() && "rotate-180"
                              )}
                            >
                              <MIcon name="expand_more" class="text-[16px]" />
                            </div>
                            <div class="flex min-w-0 flex-1 flex-col gap-0.5">
                              <span
                                data-testid={`parameter-display-${entry.key}`}
                                title={meta().displayName}
                                class="text-on-surface block min-w-0 truncate text-[13.5px] font-bold"
                              >
                                {meta().displayName}
                              </span>
                              <span
                                data-testid={`parameter-key-${entry.key}`}
                                title={entry.key}
                                class="text-outline block min-w-0 truncate font-mono text-[10px] tracking-tight"
                              >
                                {entry.key}
                              </span>
                            </div>
                          </div>
                        </td>
                        <td
                          class={cn(
                            "min-w-0 overflow-hidden",
                            isCompact() ? "px-2.5 py-2" : "px-4 py-4"
                          )}
                        >
                          <div
                            class="flex w-full min-w-0 items-center gap-2"
                            onClick={e => e.stopPropagation()}
                          >
                            <span
                              data-testid={`parameter-value-${entry.key}`}
                              title={entry.value || undefined}
                              class="text-on-surface-variant block min-w-0 flex-1 truncate font-mono"
                            >
                              {entry.value}
                            </span>
                            <button
                              onClick={() => void props.onCopyValue(entry.key, entry.value)}
                              title="Copy value"
                              class="text-outline hover:text-primary hover:bg-primary/10 flex shrink-0 cursor-pointer items-center justify-center rounded border-0 bg-transparent p-1 opacity-40 transition-all group-hover:opacity-100 focus:opacity-100"
                            >
                              <MIcon
                                name={props.copiedKey === entry.key ? "check" : "content_copy"}
                                class="text-[14px]"
                              />
                            </button>
                          </div>
                        </td>
                        <td
                          class={cn(
                            "overflow-hidden font-mono",
                            isCompact() ? "px-2.5 py-2" : "px-4 py-4"
                          )}
                        >
                          <span
                            class={cn(
                              "rounded-full text-[9px] font-bold tracking-wider uppercase",
                              isCompact() ? "px-1.5 py-0" : "px-2 py-0.5",
                              TYPE_STYLE[entry.contentType] ?? ""
                            )}
                          >
                            {entry.contentType}
                          </span>
                        </td>
                        <td
                          class={cn(
                            "overflow-hidden font-mono",
                            isCompact() ? "px-2.5 py-2" : "px-4 py-4"
                          )}
                        >
                          <span
                            class={cn(
                              "rounded-full text-[9px] font-bold tracking-wider uppercase",
                              isCompact() ? "px-1.5 py-0" : "px-2 py-0.5",
                              SCOPE_STYLE[entry.scope] ?? ""
                            )}
                          >
                            {entry.scope}
                          </span>
                        </td>
                        <td
                          class={cn(
                            "overflow-hidden text-right",
                            isCompact() ? "px-2.5 py-2" : "px-4 py-4"
                          )}
                          onClick={e => e.stopPropagation()}
                        >
                          <Show when={props.canManage}>
                            <div
                              class={cn(
                                "flex justify-end",
                                isCompact() ? "gap-0.5" : "gap-1"
                              )}
                            >
                              <button
                                data-testid={`parameter-share-${entry.key}`}
                                onClick={() => props.onShareEntry(entry)}
                                class="text-outline hover:text-primary hover:bg-primary/10 cursor-pointer rounded-lg border-0 bg-transparent p-1.5 opacity-40 transition-opacity group-hover:opacity-100 focus:opacity-100"
                                title={`Share parameter ${entry.key}`}
                                aria-label={`Share parameter ${entry.key}`}
                              >
                                <MIcon name="ios_share" class="text-[18px]" />
                              </button>
                              <button
                                data-testid={`parameter-delete-${entry.key}`}
                                onClick={() => props.onDeleteEntry(entry.key)}
                                class="text-outline hover:text-error hover:bg-error/10 cursor-pointer rounded-lg border-0 bg-transparent p-1.5 opacity-40 transition-opacity group-hover:opacity-100 focus:opacity-100"
                                title={`Delete parameter ${entry.key}`}
                                aria-label={`Delete parameter ${entry.key}`}
                              >
                                <MIcon name="delete_outline" class="text-[18px]" />
                              </button>
                            </div>
                          </Show>
                        </td>
                      </tr>
                      <Show when={isExpanded()}>
                        <tr data-testid={`parameter-accordion-${entry.key}`}>
                          <td
                            colSpan={5}
                            class={cn(
                              "bg-surface-container-lowest/30",
                              isCompact() ? "px-3 py-2.5" : "px-6 py-4"
                            )}
                          >
                            <ProjectParamEditDrawer
                              entry={props.editingEntry}
                              activeEnvName={props.activeEnvName}
                              initialDescription={props.initialDescription}
                              onClose={props.onCloseEntry}
                              onDirtyChange={props.onEditDirtyChange}
                              onSaveSettings={props.onSaveSettings}
                              isSaving={props.isSaving}
                              canManage={props.canManage}
                              historyVersions={props.historyVersions}
                              isHistoryLoading={props.isHistoryLoading}
                              isRollingBack={props.isRollingBack}
                              onRollbackVersion={props.onRollbackVersion}
                              historyLayout="desktop"
                              isReadOnly={props.isReadOnly}
                              releaseVersion={props.releaseVersion}
                            />
                          </td>
                        </tr>
                      </Show>
                    </>
                  );
                }}
              </For>
            </Show>
            <Show when={!props.isLoading && props.search && props.filteredConfig.length === 0}>
              <tr>
                <td
                  colSpan={5}
                  class={cn(
                    "text-on-surface-variant text-center text-sm",
                    isCompact() ? "py-6" : "py-10"
                  )}
                >
                  No parameters match "
                  <span class="text-on-surface font-medium">{props.search}</span>"
                </td>
              </tr>
            </Show>
          </tbody>
        </table>
        </div>
        </div>
      </Show>
    </div>
  );
}
