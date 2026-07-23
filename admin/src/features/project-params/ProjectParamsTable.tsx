import { createMediaQuery } from "@solid-primitives/media";
import { For, Show, createMemo } from "solid-js";
import { ProjectParamEditDrawer } from "../project-param-edit/ProjectParamEditDrawer";
import { MIcon } from "../../shared/ui/icons";
import { cn } from "../../shared/lib/utils";
import type { ConfigEntry, ConfigEntryVersion } from "../../types";

export interface ProjectParamsTableProps {
  isLoading: boolean;
  projectId: string;
  activeEnvName: string;
  filteredConfig: ConfigEntry[];
  editingEntry: ConfigEntry | null;
  onSelectEntry: (entry: ConfigEntry) => void;
  onShareEntry: (entry: ConfigEntry) => void;
  showShareActions?: boolean;
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
  onSaveSettings: (data: { value: string; description: string }) => void;
  isSaving: boolean;
  historyVersions: ConfigEntryVersion[];
  isHistoryLoading: boolean;
  isRollingBack: boolean;
  onRollbackVersion: (version: ConfigEntryVersion) => void;
  search: string;
  isReadOnly?: boolean;
  releaseVersion?: string;
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

  return (
    <div class="space-y-3">
      <Show when={isMobile()}>
        <div class="space-y-3">
        <Show when={props.isLoading}>
          <For each={[1, 2, 3]}>
            {() => <div class="skeleton h-36 w-full rounded-2xl" />}
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
                <article class="bg-surface-container border-outline-variant/10 overflow-hidden rounded-2xl border">
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
                    class="w-full cursor-pointer border-0 bg-transparent p-4 text-left"
                  >
                    <div class="flex items-start gap-3">
                      <div
                        class={`bg-surface-container-high text-outline mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-lg transition-transform ${
                          isExpanded() ? "rotate-180" : ""
                        }`}
                      >
                        <MIcon name="expand_more" class="text-[16px]" />
                      </div>

                      <div class="min-w-0 flex-1 space-y-3">
                        <div class="min-w-0">
                          <span
                            data-testid={`parameter-display-${entry.key}`}
                            class="text-on-surface block text-[13.5px] font-bold"
                          >
                            {meta().displayName}
                          </span>
                          <span
                            data-testid={`parameter-key-${entry.key}`}
                            class="text-outline mt-0.5 block font-mono text-[10px] tracking-tight break-all"
                          >
                            {entry.key}
                          </span>
                        </div>

                        <div class="flex flex-wrap gap-2">
                          <span
                            class={cn(
                              "rounded-full px-2 py-0.5 text-[9px] font-bold tracking-wider uppercase",
                              TYPE_STYLE[entry.contentType] ?? ""
                            )}
                          >
                            {entry.contentType}
                          </span>
                          <span
                            class={cn(
                              "rounded-full px-2 py-0.5 text-[9px] font-bold tracking-wider uppercase",
                              SCOPE_STYLE[entry.scope] ?? ""
                            )}
                          >
                            {entry.scope}
                          </span>
                        </div>

                        <div
                          class="bg-surface-container-lowest/60 flex items-center gap-2 rounded-xl px-3 py-2"
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
                    <div class="border-outline-variant/10 flex justify-end gap-1 border-t px-4 py-2">
                      <Show when={props.showShareActions !== false}>
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
                      </Show>
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
                      class="bg-surface-container-lowest/30 border-outline-variant/10 border-t px-4 py-4"
                    >
                      <ProjectParamEditDrawer
                       {...props}
                        entry={props.editingEntry}
                        onClose={props.onCloseEntry}
                      />
                    </div>
                  </Show>
                </article>
              );
            }}
          </For>
        </Show>

        <Show when={!props.isLoading && props.search && props.filteredConfig.length === 0}>
          <div class="text-on-surface-variant py-10 text-center text-sm">
            No parameters match "<span class="text-on-surface font-medium">{props.search}</span>"
          </div>
        </Show>
        </div>
      </Show>

      <Show when={!isMobile()}>
        <div class="bg-surface-container-low border-outline-variant/15 overflow-hidden rounded-xl border">
        <div class="overflow-x-auto">
        <table class="w-full border-collapse text-left text-[12px]">
          <thead class="sticky top-0 z-10">
            <tr class="border-outline-variant/15 bg-surface-container-lowest/50 border-b">
              <th class="text-outline px-6 py-3 text-[11px] font-medium tracking-[0.05em] uppercase">
                Parameter
              </th>
              <th class="text-outline px-6 py-3 text-[11px] font-medium tracking-[0.05em] uppercase">
                Value
              </th>
              <th class="text-outline px-6 py-3 text-[11px] font-medium tracking-[0.05em] uppercase">
                Type
              </th>
              <th class="text-outline px-6 py-3 text-[11px] font-medium tracking-[0.05em] uppercase">
                Scope
              </th>
              <th class="text-outline w-24 px-6 py-3 text-right text-[11px] font-medium tracking-[0.05em] uppercase">
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
                    <td class="px-6 py-4">
                      <div class="skeleton h-4 w-40 rounded" />
                    </td>
                    <td class="px-6 py-4">
                      <div class="skeleton h-4 w-32 rounded" />
                    </td>
                    <td class="px-6 py-4">
                      <div class="skeleton h-5 w-14 rounded-full" />
                    </td>
                    <td class="px-6 py-4">
                      <div class="skeleton h-5 w-14 rounded-full" />
                    </td>
                    <td class="px-6 py-4" />
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
                        <td class="px-6 py-4">
                          <div class="flex items-center gap-3">
                            <div
                              class={`bg-surface-container-high text-outline flex h-7 w-7 shrink-0 items-center justify-center rounded-lg transition-transform ${
                                isExpanded() ? "rotate-180" : ""
                              }`}
                            >
                              <MIcon name="expand_more" class="text-[16px]" />
                            </div>
                            <div class="flex flex-col gap-0.5">
                              <span
                                data-testid={`parameter-display-${entry.key}`}
                                class="text-on-surface text-[13.5px] font-bold"
                              >
                                {meta().displayName}
                              </span>
                              <span
                                data-testid={`parameter-key-${entry.key}`}
                                class="text-outline font-mono text-[10px] tracking-tight"
                              >
                                {entry.key}
                              </span>
                            </div>
                          </div>
                        </td>
                        <td class="px-6 py-4">
                          <div class="flex items-center gap-2" onClick={e => e.stopPropagation()}>
                            <span
                              data-testid={`parameter-value-${entry.key}`}
                              class="text-on-surface-variant block max-w-45 truncate font-mono"
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
                        <td class="px-6 py-4 font-mono">
                          <span
                            class={cn(
                              "rounded-full px-2 py-0.5 text-[9px] font-bold tracking-wider uppercase",
                              TYPE_STYLE[entry.contentType] ?? ""
                            )}
                          >
                            {entry.contentType}
                          </span>
                        </td>
                        <td class="px-6 py-4 font-mono">
                          <span
                            class={cn(
                              "rounded-full px-2 py-0.5 text-[9px] font-bold tracking-wider uppercase",
                              SCOPE_STYLE[entry.scope] ?? ""
                            )}
                          >
                            {entry.scope}
                          </span>
                        </td>
                        <td class="px-6 py-4 text-right" onClick={e => e.stopPropagation()}>
                          <Show when={props.canManage}>
                            <div class="flex justify-end gap-1">
                              <Show when={props.showShareActions !== false}>
                                <button
                                  data-testid={`parameter-share-${entry.key}`}
                                  onClick={() => props.onShareEntry(entry)}
                                  class="text-outline hover:text-primary hover:bg-primary/10 cursor-pointer rounded-lg border-0 bg-transparent p-1.5 opacity-40 transition-opacity group-hover:opacity-100 focus:opacity-100"
                                  title={`Share parameter ${entry.key}`}
                                  aria-label={`Share parameter ${entry.key}`}
                                >
                                  <MIcon name="ios_share" class="text-[18px]" />
                                </button>
                              </Show>
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
                          <td colSpan={5} class="bg-surface-container-lowest/30 px-6 py-4">
                            <ProjectParamEditDrawer
                              entry={props.editingEntry}
                              activeEnvName={props.activeEnvName}
                              initialDescription={props.initialDescription}
                              onClose={props.onCloseEntry}
                              onSaveSettings={props.onSaveSettings}
                              isSaving={props.isSaving}
                              canManage={props.canManage}
                              historyVersions={props.historyVersions}
                              isHistoryLoading={props.isHistoryLoading}
                              isRollingBack={props.isRollingBack}
                              onRollbackVersion={props.onRollbackVersion}
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
                <td colSpan={5} class="text-on-surface-variant py-10 text-center text-sm">
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
