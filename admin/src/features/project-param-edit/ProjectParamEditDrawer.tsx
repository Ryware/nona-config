import type { JSX } from "solid-js";
import { createEffect, createMemo, createSignal, For, Show } from "solid-js";
import { Button } from "../../shared/ui/button";
import { MIcon } from "../../shared/ui/icons";
import { Input } from "../../shared/ui/input";
import { Label } from "../../shared/ui/label";
import { Select } from "../../shared/ui/select";
import { VisualJsonEditor } from "../../shared/ui/visual-json-editor";
import type { ConfigEntry, ConfigEntryVersion } from "../../types";

interface ProjectParamEditDrawerProps {
  entry: ConfigEntry | null;
  activeEnvName: string;
  initialDescription: string;
  onClose: () => void;
  onSaveSettings: (data: { value: string; description: string }) => void;
  isSaving: boolean;
  canManage: boolean;
  historyVersions: ConfigEntryVersion[];
  isHistoryLoading: boolean;
  isRollingBack: boolean;
  onRollbackVersion: (version: ConfigEntryVersion) => void;
}

interface FieldRowProps {
  label: string;
  value: string | undefined;
  changed: boolean;
  initial: boolean;
  mono?: boolean;
}

function FieldRow(props: FieldRowProps): JSX.Element {
  return (
    <div class="flex items-start gap-3 py-1">
      <span class="text-outline w-24 shrink-0 pt-px text-[11px] leading-snug font-medium">
        {props.label}
      </span>
      <span
        class={`min-w-0 flex-1 text-[12px] leading-relaxed break-all ${props.mono ? "font-mono" : ""} ${
          props.changed ? "text-on-surface" : "text-on-surface-variant/50"
        }`}
      >
        {props.value ? props.value : <span class="text-outline/40 italic">-</span>}
      </span>
      <Show when={props.changed}>
        <span
          class={`mt-0.5 shrink-0 rounded-full px-1.5 py-0.5 text-[9px] font-medium ${
            props.initial ? "bg-secondary/10 text-secondary" : "bg-primary/10 text-primary"
          }`}
        >
          {props.initial ? "initial" : "changed"}
        </span>
      </Show>
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

  createEffect(() => {
    const entry = props.entry;
    if (entry) {
      setEditVal(entry.value);
      setEditDescription(props.initialDescription);
      setActiveDrawerTab("settings");
    }
  });

  const isValidJson = (str: string): boolean => {
    try {
      JSON.parse(str);
      return true;
    } catch {
      return false;
    }
  };

  const isEditInvalid = () => {
    const entry = props.entry;
    if (entry && entry.contentType === "json") {
      return !isValidJson(editVal());
    }
    return false;
  };

  const handleSave = () => {
    if (!props.canManage) return;

    props.onSaveSettings({
      value: editVal().trim(),
      description: editDescription().trim()
    });
  };

  return (
    <Show when={props.entry}>
      {(() => {
        const entry = props.entry!;

        return (
          <div
            data-testid="parameter-edit-drawer"
          >
            <div class="bg-surface-container/60 mb-5 flex gap-1 rounded-xl p-1">
              <button
                type="button"
                onClick={() => setActiveDrawerTab("settings")}
                class={`flex-1 cursor-pointer rounded-lg border-0 py-1.5 text-[12px] font-medium transition-all ${
                  activeDrawerTab() === "settings"
                    ? "bg-primary text-on-primary"
                    : "text-outline hover:text-on-surface bg-transparent"
                }`}
              >
                Settings
              </button>
              <button
                type="button"
                onClick={() => setActiveDrawerTab("history")}
                class={`flex-1 cursor-pointer rounded-lg border-0 py-1.5 text-[12px] font-medium transition-all ${
                  activeDrawerTab() === "history"
                    ? "bg-primary text-on-primary"
                    : "text-outline hover:text-on-surface bg-transparent"
                }`}
              >
                History ({props.historyVersions.length})
              </button>
            </div>

            <div class="pr-1 pl-1">
              <Show when={activeDrawerTab() === "settings"}>
                <div class="grid gap-5 xl:grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)]">
                  <div class="space-y-5">
                    <div class="bg-surface-container-high/40 border-outline-variant/15 flex flex-wrap items-center gap-2 rounded-xl border px-3 py-2.5">
                      <span class="text-outline text-[11px] font-medium tracking-[0.05em]">
                        Context
                      </span>
                      <span class="bg-primary/10 text-primary border-primary/20 rounded-full border px-2.5 py-0.5 font-mono text-[11px]">
                        {props.activeEnvName}
                      </span>
                      <span class="text-outline/50 text-[11px]">•</span>
                      <span class="bg-secondary/10 text-secondary border-secondary/20 rounded-full border px-2.5 py-0.5 font-mono text-[11px]">
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
                        class="bg-surface-container-lowest border-outline-variant/20 focus:border-primary focus:ring-primary/20 text-on-surface placeholder:text-outline/60 hover:border-outline-variant/30 w-full resize-none rounded-xl border px-4 py-2.5 text-[13px] transition-all outline-none focus:ring-2"
                        placeholder="Describe what this setting controls..."
                        data-testid="parameter-edit-description-input"
                      />
                    </div>

                    <div class="space-y-2">
                      <Label class="mb-0">Datatype</Label>
                      <div class="text-primary bg-primary/5 border-primary/15 inline-flex items-center gap-1.5 rounded-lg border px-3 py-1.5 font-mono text-[11px]">
                        <MIcon name="data_object" class="text-[14px]" />
                        {entry.contentType}
                      </div>
                    </div>

                  </div>

                  <div class="space-y-2">
                    <Label class="mb-0">Value</Label>
                    <Show when={entry.contentType === "boolean"}>
                      <Select
                        value={editVal()}
                        onChange={val => setEditVal(val)}
                        disabled={!props.canManage}
                        options={[
                          { value: "true", label: "True / Active" },
                          { value: "false", label: "False / Inactive" }
                        ]}
                      />
                    </Show>
                    <Show when={entry.contentType === "number"}>
                      <Input
                        type="number"
                        value={editVal()}
                        onInput={(e: InputEvent & { currentTarget: HTMLInputElement }) =>
                          setEditVal(e.currentTarget.value)
                        }
                        disabled={!props.canManage}
                        leftIcon="pin"
                      />
                    </Show>
                    <Show when={entry.contentType === "json"}>
                      <VisualJsonEditor
                        id="config-entry-edit-value"
                        value={editVal()}
                        onChange={props.canManage ? setEditVal : () => undefined}
                      />
                    </Show>
                    <Show when={entry.contentType === "text"}>
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

              <Show when={activeDrawerTab() === "history"}>
                <div>
                  <p class="text-outline mb-5 text-[11px] font-medium tracking-[0.05em]">
                    Version timeline
                  </p>
                  <Show
                    when={!props.isHistoryLoading}
                    fallback={
                      <div class="space-y-5">
                        <For each={[1, 2, 3]}>
                          {() => (
                            <div class="space-y-3">
                              <div class="skeleton h-4 w-40 rounded" />
                              <div class="skeleton h-12 w-full rounded-lg" />
                            </div>
                          )}
                        </For>
                      </div>
                    }
                  >
                    <Show
                      when={props.historyVersions.length > 0}
                      fallback={
                        <div class="text-outline py-12 text-center text-[12px]">
                          No version history.
                        </div>
                      }
                    >
                      <div class="border-outline-variant/20 relative space-y-7 border-l pl-5">
                        <For each={props.historyVersions}>
                          {(version, index) => {
                            const changes = createMemo(() => {
                              const prev = props.historyVersions[index() + 1];
                              return prev
                                ? {
                                    isInitial: false,
                                    value: version.value !== prev.value,
                                    contentType: version.contentType !== prev.contentType,
                                    scope: version.scope !== prev.scope
                                  }
                                : {
                                    isInitial: true,
                                    value: true,
                                    contentType: true,
                                    scope: true
                                  };
                            });
                            const isActive = () => version.version === entry.activeVersion;

                            return (
                              <div class="relative">
                                <div
                                  class={`ring-surface-container-low absolute top-1.5 -left-6.25 h-2 w-2 rounded-full ring-2 ${
                                    isActive() ? "bg-secondary" : "bg-primary/70"
                                  }`}
                                />

                                <div class="mb-3 flex items-start justify-between gap-3">
                                  <div class="min-w-0">
                                    <div class="flex flex-wrap items-center gap-2">
                                      <span class="text-on-surface font-mono text-[13px] font-bold">
                                        v{version.version}
                                      </span>
                                      <Show when={isActive()}>
                                        <span class="bg-secondary/10 text-secondary border-secondary/20 rounded-full border px-1.5 py-0.5 text-[9px] font-bold tracking-wider uppercase">
                                          active
                                        </span>
                                      </Show>
                                    </div>
                                    <span class="text-on-surface-variant block truncate text-[12px]">
                                      {version.actor}
                                    </span>
                                  </div>
                                  <span class="text-outline shrink-0 font-mono text-[10px]">
                                    {fmtRevDate(version.createdAt)}
                                  </span>
                                </div>

                                <div class="divide-outline-variant/8 space-y-0 divide-y">
                                  <FieldRow
                                    label="Value"
                                    value={version.value}
                                    changed={changes().value}
                                    initial={changes().isInitial}
                                    mono
                                  />
                                  <FieldRow
                                    label="Datatype"
                                    value={version.contentType}
                                    changed={changes().contentType}
                                    initial={changes().isInitial}
                                    mono
                                  />
                                  <FieldRow
                                    label="Scope"
                                    value={version.scope}
                                    changed={changes().scope}
                                    initial={changes().isInitial}
                                    mono
                                  />
                                </div>

                                <Show when={props.canManage && !isActive()}>
                                  <div class="mt-3 flex justify-end">
                                    <button
                                      type="button"
                                      onClick={() => props.onRollbackVersion(version)}
                                      disabled={props.isRollingBack}
                                      class="text-primary hover:text-primary-container flex cursor-pointer items-center gap-1.5 border-0 bg-transparent px-0 text-[12px] font-medium transition-colors disabled:cursor-not-allowed disabled:opacity-40"
                                    >
                                      <MIcon name="history" class="text-[14px]" />
                                      Rollback to v{version.version}
                                    </button>
                                  </div>
                                </Show>
                              </div>
                            );
                          }}
                        </For>
                      </div>
                    </Show>
                  </Show>
                </div>
              </Show>
            </div>

            <Show
              when={activeDrawerTab() === "settings"}
              fallback={
                <div class="border-outline-variant/15 mt-4 flex justify-end border-t pt-4">
                  <Button type="button" variant="outline" onClick={() => props.onClose()}>
                    <MIcon name="close" class="text-[16px]" />
                    Close
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
