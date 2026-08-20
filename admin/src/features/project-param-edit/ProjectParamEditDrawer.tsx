import type { JSX } from "solid-js";
import { createEffect, createMemo, createSignal, For, onCleanup, Show } from "solid-js";
import { useClipboard } from "../../shared/hooks/useClipboard";
import { Button } from "../../shared/ui/button";
import { MIcon } from "../../shared/ui/icons";
import { Input } from "../../shared/ui/input";
import { Label } from "../../shared/ui/label";
import { Select } from "../../shared/ui/select";
import { VisualJsonEditor } from "../../shared/ui/visual-json-editor";
import type { ConfigEntry, ConfigEntryVersion } from "../../types";
import { isValidConfigEntryValue } from "./config-entry-value";
import { TooltipLabel, Tooltip, TooltipTrigger } from "../../shared/ui/tooltip";
import { scopeTooltip, tooltipCopy } from "../../shared/lib/tooltip-copy";

interface ProjectParamEditDrawerProps {
  entry: ConfigEntry | null;
  activeEnvName: string;
  initialDescription: string;
  onClose: () => void;
  onSaveSettings: (data: {
    value: string;
    description: string;
    contentType: ConfigEntry["contentType"];
    scope: ConfigEntry["scope"];
  }) => void;
  isSaving: boolean;
  canManage: boolean;
  historyVersions: ConfigEntryVersion[];
  isHistoryLoading: boolean;
  isRollingBack: boolean;
  onRollbackVersion: (version: ConfigEntryVersion) => void;
  historyLayout: "mobile" | "desktop";
  isReadOnly?: boolean;
  releaseVersion?: string;
  onDirtyChange: (dirty: boolean) => void;
}

interface FieldRowProps {
  value: string | undefined;
  mono?: boolean;
}

function FieldRow(props: FieldRowProps): JSX.Element {
  return (
    <Tooltip content={props.value || "No value"}>
      <TooltipTrigger as="span" tabindex="0" data-tooltip-trigger class={`text-on-surface block min-w-0 truncate text-[11px] leading-tight md:text-[12px] ${props.mono ? "font-mono" : ""}`}>
        {props.value ? props.value : <span class="text-outline/40 italic">-</span>}
      </TooltipTrigger>
    </Tooltip>
  );
}

interface HistoryValueFieldProps {
  value: string;
  version: number;
  copied: boolean;
  onCopy: () => void;
}

function HistoryValueField(props: HistoryValueFieldProps): JSX.Element {
  return (
    <div
      data-testid={`parameter-history-value-v${props.version}`}
      class="bg-surface-container-lowest/60 flex w-full min-w-0 items-center gap-1.5 rounded-lg px-2 py-1.5"
    >
      <Tooltip content={props.value || "No value"}>
      <TooltipTrigger as="span" tabindex="0" data-tooltip-trigger
        class="text-on-surface min-w-0 flex-1 truncate font-mono text-[11px] leading-tight md:text-[12px]"
      >
        {props.value || <span class="text-outline/40 italic">-</span>}
      </TooltipTrigger>
      </Tooltip>
      <button
        type="button"
        onClick={event => {
          event.stopPropagation();
          props.onCopy();
        }}
        title={`Copy value from v${props.version}`}
        aria-label={`Copy value from v${props.version}`}
        class="text-outline hover:text-primary hover:bg-primary/10 flex shrink-0 cursor-pointer items-center justify-center rounded border-0 bg-transparent p-1 transition-colors"
      >
        <MIcon name={props.copied ? "check" : "content_copy"} class="text-[13px]" />
      </button>
    </div>
  );
}

function fmtRevDate(timestamp: string): string {
  const d = new Date(timestamp);
  const date = d.toLocaleDateString("ru-RU", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric"
  });
  const time = d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
  return `${date} ${time}`;
}

export function ProjectParamEditDrawer(props: ProjectParamEditDrawerProps) {
  const [activeDrawerTab, setActiveDrawerTab] = createSignal<"settings" | "history">("settings");
  const [editVal, setEditVal] = createSignal("");
  const [editDescription, setEditDescription] = createSignal("");
  const [editContentType, setEditContentType] = createSignal<ConfigEntry["contentType"]>("text");
  const [editScope, setEditScope] = createSignal<ConfigEntry["scope"]>("all");
  const [expandedHistoryVersion, setExpandedHistoryVersion] = createSignal<number | null>(null);
  const { copy: copyHistoryValue, copied: copiedHistoryValue } = useClipboard();
  const prettyValue = createMemo(() => {
    const entry = props.entry;
    if (!entry) return "";

    if (entry.contentType !== "json") {
      return entry.value;
    }

    try {
      return JSON.stringify(JSON.parse(entry.value), null, 2);
    } catch {
      return entry.value;
    }
  });

  createEffect(() => {
    const entry = props.entry;
    if (entry) {
      setEditVal(entry.value);
      setEditDescription(props.initialDescription);
      setEditContentType(entry.contentType);
      setEditScope(entry.scope);
      setActiveDrawerTab("settings");
      setExpandedHistoryVersion(
        props.historyVersions.some(version => version.version === entry.activeVersion)
          ? entry.activeVersion
          : (props.historyVersions[0]?.version ?? entry.activeVersion)
      );
    }
  });

  const isEditInvalid = () => !isValidConfigEntryValue(editContentType(), editVal());
  const isDirty = createMemo(() => {
    const entry = props.entry;
    if (!entry || props.isReadOnly) return false;

    return (
      editVal() !== entry.value ||
      editDescription() !== props.initialDescription ||
      editContentType() !== entry.contentType ||
      editScope() !== entry.scope
    );
  });

  createEffect(() => props.onDirtyChange(isDirty()));
  onCleanup(() => props.onDirtyChange(false));

  const handleSave = () => {
    if (!props.canManage) return;

    props.onSaveSettings({
      value: editVal().trim(),
      description: editDescription().trim(),
      contentType: editContentType(),
      scope: editScope()
    });
  };

  return (
    <Show when={props.entry}>
      {(() => {
        const entry = props.entry!;

        return (
          <div data-testid="parameter-edit-drawer">
            <Show when={!props.isReadOnly}>
              <div class="bg-surface-container/60 mb-5 grid grid-cols-2 gap-1 rounded-xl p-1">
                <button
                  type="button"
                  onClick={() => setActiveDrawerTab("settings")}
                  class={`min-w-0 cursor-pointer rounded-lg border-0 px-2 py-1.5 text-[12px] font-medium transition-all sm:text-[13px] ${
                    activeDrawerTab() === "settings"
                      ? "bg-primary text-on-primary"
                      : "text-outline hover:text-on-surface bg-transparent"
                  }`}
                >
                  Settings
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setActiveDrawerTab("history");
                    setExpandedHistoryVersion(
                      props.historyVersions.some(version => version.version === entry.activeVersion)
                        ? entry.activeVersion
                        : (props.historyVersions[0]?.version ?? null)
                    );
                  }}
                  class={`min-w-0 cursor-pointer rounded-lg border-0 px-2 py-1.5 text-[12px] font-medium transition-all sm:text-[13px] ${
                    activeDrawerTab() === "history"
                      ? "bg-primary text-on-primary"
                      : "text-outline hover:text-on-surface bg-transparent"
                  }`}
                >
                  <span class="truncate">History</span>
                  <span class="ml-1 text-[11px] opacity-80 sm:text-[12px]">
                    ({props.historyVersions.length})
                  </span>
                </button>
              </div>
            </Show>

            <div class="pr-1 pl-1">
              <Show when={props.isReadOnly}>
                <div class="grid gap-5 xl:grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)]">
                  <div class="space-y-5">
                    <div class="bg-surface-container-high/40 border-outline-variant/15 flex flex-wrap items-center gap-2 rounded-xl border px-3 py-2.5">
                      <span class="text-outline text-[12px] font-medium tracking-[0.05em]">
                        Context
                      </span>
                      <span class="bg-primary/10 text-primary border-primary/20 rounded-full border px-2.5 py-0.5 font-mono text-[12px]">
                        {props.activeEnvName}
                      </span>
                      <span class="text-outline/50 text-[12px]">•</span>
                      <span class="bg-secondary/10 text-secondary border-secondary/20 rounded-full border px-2.5 py-0.5 font-mono text-[12px]">
                        release {props.releaseVersion}
                      </span>
                    </div>

                    <div class="space-y-2">
                      <Label class="mb-0">Description</Label>
                      <div class="bg-surface-container-lowest border-outline-variant/20 text-on-surface min-h-[88px] rounded-xl border px-4 py-3 text-[14px] leading-relaxed">
                        {editDescription().trim() || (
                          <span class="text-outline/60">No description provided.</span>
                        )}
                      </div>
                    </div>

                    <div class="grid gap-4 sm:grid-cols-2">
                      <div class="space-y-2">
                        <TooltipLabel class="mb-0" content={tooltipCopy.datatype}>Datatype</TooltipLabel>
                        <div class="text-primary bg-primary/5 border-primary/15 inline-flex items-center gap-1.5 rounded-lg border px-3 py-1.5 font-mono text-[12px]">
                          <MIcon name="data_object" class="text-[14px]" />
                          {entry.contentType}
                        </div>
                      </div>

                      <div class="space-y-2">
                        <TooltipLabel class="mb-0" content={scopeTooltip(entry.scope)}>Scope</TooltipLabel>
                        <div class="text-secondary bg-secondary/5 border-secondary/15 inline-flex items-center gap-1.5 rounded-lg border px-3 py-1.5 font-mono text-[12px]">
                          <MIcon name="public" class="text-[14px]" />
                          {entry.scope}
                        </div>
                      </div>
                    </div>
                  </div>

                  <div class="space-y-2">
                    <Label class="mb-0">Value</Label>
                    <pre class="bg-surface-container-lowest border-outline-variant/20 text-on-surface min-h-[220px] overflow-x-auto rounded-xl border px-4 py-3 font-mono text-[13px] leading-relaxed break-all whitespace-pre-wrap">
                      {prettyValue()}
                    </pre>
                  </div>
                </div>
              </Show>

              <Show when={!props.isReadOnly && activeDrawerTab() === "settings"}>
                <div class="grid gap-5 xl:grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)]">
                  <div class="space-y-5">
                    <div class="bg-surface-container-high/40 border-outline-variant/15 flex flex-wrap items-center gap-2 rounded-xl border px-3 py-2.5">
                      <span class="text-outline text-[12px] font-medium tracking-[0.05em]">
                        Context
                      </span>
                      <span class="bg-primary/10 text-primary border-primary/20 rounded-full border px-2.5 py-0.5 font-mono text-[12px]">
                        {props.activeEnvName}
                      </span>
                      <span class="text-outline/50 text-[12px]">•</span>
                      <span class="bg-secondary/10 text-secondary border-secondary/20 rounded-full border px-2.5 py-0.5 font-mono text-[12px]">
                        active v{entry.activeVersion}
                      </span>
                    </div>

                    <div class="space-y-2">
                      <Label class="mb-0">Description</Label>
                      <textarea
                        value={editDescription()}
                        onInput={(e: InputEvent & { currentTarget: HTMLTextAreaElement }) =>
                          setEditDescription(e.currentTarget.value)
                        }
                        disabled={!props.canManage}
                        rows={3}
                        maxLength={500}
                        class="bg-surface-container-lowest border-outline-variant/20 focus:border-primary focus:ring-primary/20 text-on-surface placeholder:text-outline/60 hover:border-outline-variant/30 w-full resize-none rounded-xl border px-4 py-2.5 text-[14px] transition-all outline-none focus:ring-2"
                        placeholder="Describe what this setting controls..."
                        data-testid="parameter-edit-description-input"
                      />
                    </div>

                    <div class="grid gap-4 sm:grid-cols-2">
                      <div class="space-y-2">
                        <TooltipLabel class="mb-0" content={tooltipCopy.datatype}>Datatype</TooltipLabel>
                        <Select
                          id="config-entry-edit-content-type"
                          aria-label="Datatype"
                          value={editContentType()}
                          onChange={val => {
                            setEditContentType(val as ConfigEntry["contentType"]);
                            setEditVal("");
                          }}
                          disabled={!props.canManage}
                          options={["text", "number", "boolean", "json"]}
                        />
                      </div>

                      <div class="space-y-2">
                        <TooltipLabel class="mb-0" content={tooltipCopy.scope}>Scope</TooltipLabel>
                        <Select
                          value={editScope()}
                          onChange={val => setEditScope(val as ConfigEntry["scope"])}
                          disabled={!props.canManage}
                          options={[
                            { value: "all", label: "All" },
                            { value: "client", label: "Client" },
                            { value: "server", label: "Server" }
                          ]}
                        />
                      </div>
                    </div>
                  </div>

                  <div class="space-y-2">
                    <Label class="mb-0">Value</Label>
                    <Show when={editContentType() === "boolean"}>
                      <Select
                        value={editVal()}
                        onChange={val => setEditVal(val)}
                        disabled={!props.canManage}
                        placeholder="Select status..."
                        options={[
                          { value: "true", label: "True / Active" },
                          { value: "false", label: "False / Inactive" }
                        ]}
                      />
                    </Show>
                    <Show when={editContentType() === "number"}>
                      <Input
                        data-testid="parameter-edit-value-input"
                        type="number"
                        value={editVal()}
                        onInput={(e: InputEvent & { currentTarget: HTMLInputElement }) =>
                          setEditVal(e.currentTarget.value)
                        }
                        disabled={!props.canManage}
                        leftIcon="pin"
                      />
                    </Show>
                    <Show when={editContentType() === "json"}>
                      <VisualJsonEditor
                        id="config-entry-edit-value"
                        value={editVal()}
                        onChange={props.canManage ? setEditVal : () => undefined}
                      />
                    </Show>
                    <Show when={editContentType() === "text"}>
                      <Input
                        type="text"
                        value={editVal()}
                        onInput={(e: InputEvent & { currentTarget: HTMLInputElement }) =>
                          setEditVal(e.currentTarget.value)
                        }
                        disabled={!props.canManage}
                        leftIcon="text_fields"
                        data-testid="parameter-edit-value-input"
                      />
                    </Show>
                  </div>
                </div>
              </Show>

              <Show when={!props.isReadOnly && activeDrawerTab() === "history"}>
                <div>
                  <p class="text-outline mb-5 text-[12px] font-medium tracking-[0.05em]">
                    Version timeline
                  </p>
                  <Show
                    when={!props.isHistoryLoading}
                    fallback={
                      <div class="space-y-2">
                        <For each={[1, 2, 3]}>
                          {() => (
                            <div class="flex items-center gap-4 py-2">
                              <div class="skeleton h-4 w-40 rounded" />
                              <div class="skeleton h-4 flex-1 rounded" />
                            </div>
                          )}
                        </For>
                      </div>
                    }
                  >
                    <Show
                      when={props.historyVersions.length > 0}
                      fallback={
                        <div class="text-outline py-12 text-center text-[13px]">
                          No version history.
                        </div>
                      }
                    >
                      <Show when={props.historyLayout === "desktop"}>
                        <div class="border-outline-variant/20 relative border-l pl-4">
                          <div
                            data-testid="parameter-history-header"
                            class="text-outline border-outline-variant/15 mb-3 grid min-w-0 grid-cols-[minmax(0,1fr)_6rem_5rem_8rem] gap-x-2 border-b pb-2 text-[11px] leading-none font-medium tracking-[0.05em] uppercase"
                          >
                            <span class="min-w-0">Value</span>
                            <span class="min-w-0">Datatype</span>
                            <span class="min-w-0">Scope</span>
                            <span class="min-w-0 text-right">Rollback</span>
                          </div>
                          <div class="divide-outline-variant/10 divide-y">
                            <For each={props.historyVersions}>
                              {(version, index) => {
                                const isActive = () => version.version === entry.activeVersion;

                                return (
                                  <div class="relative py-3 first:pt-0 last:pb-0">
                                    <div
                                      class={`ring-surface-container-low absolute -left-5 h-2 w-2 rounded-full ring-2 ${
                                        index() === 0 ? "top-1" : "top-4"
                                      } ${isActive() ? "bg-secondary" : "bg-primary/70"}`}
                                    />

                                    <div class="mb-2 grid min-w-0 grid-cols-[minmax(0,1fr)_auto] items-start gap-2">
                                      <div
                                        data-testid={`parameter-history-desktop-identity-v${version.version}`}
                                        class="min-w-0"
                                      >
                                        <div class="flex min-w-0 items-center gap-1.5">
                                          <span class="text-on-surface shrink-0 font-mono text-[13px] font-bold">
                                            v{version.version}
                                          </span>
                                          <Show when={isActive()}>
                                            <span class="bg-secondary/10 text-secondary border-secondary/20 shrink-0 rounded-full border px-1.5 py-px text-[9px] font-bold tracking-wider uppercase">
                                              active
                                            </span>
                                          </Show>
                                        </div>
                                        <span
                                          class="text-on-surface-variant mt-0.5 block min-w-0 truncate text-[11px]"
                                          title={version.actor}
                                        >
                                          {version.actor}
                                        </span>
                                      </div>
                                      <span class="text-outline text-right font-mono text-[10px] whitespace-nowrap">
                                        {fmtRevDate(version.createdAt)}
                                      </span>
                                    </div>

                                    <div
                                      data-testid={`parameter-history-desktop-fields-v${version.version}`}
                                      class="grid min-w-0 grid-cols-[minmax(0,1fr)_6rem_5rem_8rem] gap-x-2"
                                    >
                                      <div class="min-w-0 pr-12">
                                        <HistoryValueField
                                          value={version.value}
                                          version={version.version}
                                          copied={copiedHistoryValue() === version.value}
                                          onCopy={() => void copyHistoryValue(version.value)}
                                        />
                                      </div>
                                      <div class="min-w-0">
                                        <FieldRow value={version.contentType} mono />
                                      </div>
                                      <div class="min-w-0">
                                        <FieldRow value={version.scope} mono />
                                      </div>
                                      <div class="flex h-4 min-w-0 items-center justify-end">
                                        <Show when={props.canManage && !isActive()}>
                                          <button
                                            type="button"
                                            onClick={() => props.onRollbackVersion(version)}
                                            disabled={props.isRollingBack}
                                            class="text-primary hover:text-primary-container flex min-w-0 cursor-pointer items-center gap-1 border-0 bg-transparent px-0 text-[12px] font-medium transition-colors disabled:cursor-not-allowed disabled:opacity-40"
                                          >
                                            <MIcon name="history" class="shrink-0 text-[13px]" />
                                            <span class="truncate">
                                              Rollback to v{version.version}
                                            </span>
                                          </button>
                                        </Show>
                                      </div>
                                    </div>
                                  </div>
                                );
                              }}
                            </For>
                          </div>
                        </div>
                      </Show>

                      <Show when={props.historyLayout === "mobile"}>
                        <div data-testid="parameter-history-mobile-list" class="space-y-2">
                          <For each={props.historyVersions}>
                            {version => {
                              const isActive = () => version.version === entry.activeVersion;
                              const isExpanded = () => expandedHistoryVersion() === version.version;
                              const triggerId = `parameter-history-trigger-v${version.version}`;
                              const panelId = `parameter-history-panel-v${version.version}`;

                              return (
                                <article class="bg-surface-container-low border-outline-variant/15 overflow-hidden rounded-xl border">
                                  <button
                                    id={triggerId}
                                    type="button"
                                    aria-label={`Version v${version.version} details`}
                                    aria-expanded={isExpanded()}
                                    aria-controls={panelId}
                                    onClick={() =>
                                      setExpandedHistoryVersion(current =>
                                        current === version.version ? null : version.version
                                      )
                                    }
                                    class="hover:bg-surface-container-high/40 grid w-full cursor-pointer grid-cols-[minmax(0,1fr)_auto_1.25rem] items-center gap-2 border-0 bg-transparent px-3 py-2.5 text-left transition-colors"
                                  >
                                    <span
                                      data-testid={`parameter-history-mobile-identity-v${version.version}`}
                                      class="min-w-0"
                                    >
                                      <span class="flex min-w-0 items-center gap-1.5">
                                        <span class="text-on-surface shrink-0 font-mono text-[13px] font-bold">
                                          v{version.version}
                                        </span>
                                        <Show when={isActive()}>
                                          <span class="bg-secondary/10 text-secondary border-secondary/20 shrink-0 rounded-full border px-1.5 py-px text-[8px] font-bold tracking-wider uppercase">
                                            active
                                          </span>
                                        </Show>
                                      </span>
                                      <span
                                        title={version.actor}
                                        class="text-on-surface-variant mt-0.5 block truncate text-[10px]"
                                      >
                                        {version.actor}
                                      </span>
                                    </span>
                                    <span class="text-outline text-right font-mono text-[9px] whitespace-nowrap">
                                      {fmtRevDate(version.createdAt)}
                                    </span>
                                    <MIcon
                                      name="expand_more"
                                      class={`text-outline text-[16px] transition-transform ${
                                        isExpanded() ? "rotate-180" : ""
                                      }`}
                                    />
                                  </button>

                                  <Show when={isExpanded()}>
                                    <div
                                      id={panelId}
                                      role="region"
                                      aria-labelledby={triggerId}
                                      data-testid={`parameter-history-mobile-panel-v${version.version}`}
                                      class="border-outline-variant/10 grid grid-cols-2 gap-x-3 gap-y-3 border-t px-3 py-3"
                                    >
                                      <div class="col-span-2 min-w-0">
                                        <span class="text-outline mb-1 block text-[9px] font-medium tracking-[0.05em] uppercase">
                                          Value
                                        </span>
                                        <HistoryValueField
                                          value={version.value}
                                          version={version.version}
                                          copied={copiedHistoryValue() === version.value}
                                          onCopy={() => void copyHistoryValue(version.value)}
                                        />
                                      </div>
                                      <div class="min-w-0">
                                        <span class="text-outline mb-1 block text-[9px] font-medium tracking-[0.05em] uppercase">
                                          Datatype
                                        </span>
                                        <FieldRow value={version.contentType} mono />
                                      </div>
                                      <div class="min-w-0">
                                        <span class="text-outline mb-1 block text-[9px] font-medium tracking-[0.05em] uppercase">
                                          Scope
                                        </span>
                                        <FieldRow value={version.scope} mono />
                                      </div>
                                      <Show when={props.canManage && !isActive()}>
                                        <div class="col-span-2 flex justify-end">
                                          <button
                                            type="button"
                                            onClick={() => props.onRollbackVersion(version)}
                                            disabled={props.isRollingBack}
                                            class="text-primary hover:text-primary-container flex cursor-pointer items-center gap-1 border-0 bg-transparent px-0 text-[11px] font-medium transition-colors disabled:cursor-not-allowed disabled:opacity-40"
                                          >
                                            <MIcon name="history" class="text-[13px]" />
                                            Rollback to v{version.version}
                                          </button>
                                        </div>
                                      </Show>
                                    </div>
                                  </Show>
                                </article>
                              );
                            }}
                          </For>
                        </div>
                      </Show>
                    </Show>
                  </Show>
                </div>
              </Show>
            </div>

            <Show
              when={!props.isReadOnly && activeDrawerTab() === "settings"}
              fallback={
                <div class="border-outline-variant/15 mt-4 flex justify-end border-t pt-4">
                  <Button type="button" variant="outline" onClick={() => props.onClose()}>
                    <MIcon name="close" class="text-[16px]" />
                    {props.isReadOnly ? "Back" : "Close"}
                  </Button>
                </div>
              }
            >
              <div class="border-outline-variant/15 mt-4 flex justify-end gap-3 border-t pt-4">
                <Show when={props.canManage}>
                  <Button
                    data-testid="parameter-edit-save-button"
                    type="button"
                    onClick={handleSave}
                    disabled={props.isSaving || isEditInvalid()}
                  >
                    <MIcon name="save" class="text-[16px]" />
                    {props.isSaving ? "Saving..." : "Save"}
                  </Button>
                </Show>
                <Button
                  data-testid="parameter-edit-cancel-button"
                  type="button"
                  variant="outline"
                  onClick={() => props.onClose()}
                >
                  <MIcon name="close" class="text-[16px]" />
                  Cancel
                </Button>
              </div>
            </Show>
          </div>
        );
      })()}
    </Show>
  );
}
