import { Dialog } from "@kobalte/core/dialog";
import { For, Show, createEffect, createMemo, createSignal, on } from "solid-js";
import { Button } from "../../shared/ui/button";
import { MIcon } from "../../shared/ui/icons";
import { Input } from "../../shared/ui/input";
import { Label } from "../../shared/ui/label";
import { Select } from "../../shared/ui/select";
import { VisualJsonEditor } from "../../shared/ui/visual-json-editor";
import type { ConfigEntry, ConfigEntryVersion } from "../../types";
import { ParameterValueEditor } from "../project-params/ParameterValueEditor";
import { parameterName } from "../project-params/parameter-tree";
import {
  getConfigEntryValueError,
  isDisallowedConfigEntryKeyPress,
  readConfigEntryKeyInput,
  validateConfigEntryDraft
} from "./config-entry-value";

export type ParameterPanelMode = "create" | "live" | "amend" | "snapshot";

export interface ParameterPanelSaveData {
  key: string;
  value: string;
  description: string;
  contentType: ConfigEntry["contentType"];
  scope: ConfigEntry["scope"];
  unit: string | null;
}

interface ProjectParamPanelProps {
  open: boolean;
  mode: ParameterPanelMode;
  entry: ConfigEntry | null;
  projectId: string;
  environmentName: string;
  releaseVersion?: string;
  existingEntries: ConfigEntry[];
  canManage: boolean;
  initialDescription?: string;
  isSaving: boolean;
  historyVersions: ConfigEntryVersion[];
  isHistoryLoading: boolean;
  isHistoryActionPending: boolean;
  shareEnabled: boolean;
  shareDisabledReason?: string;
  onRequestClose: () => void;
  onDirtyChange: (dirty: boolean) => void;
  onSave: (data: ParameterPanelSaveData) => Promise<ConfigEntry | void>;
  onHistoryOpen: (key: string) => void;
  onHistoryAction: (version: ConfigEntryVersion) => Promise<ConfigEntry | void> | void;
  onShare: (entry: ConfigEntry) => void;
}

function formatJson(value: string) {
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

function formatDate(value: string) {
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime())
    ? value
    : parsed.toLocaleString([], {
        year: "numeric",
        month: "short",
        day: "2-digit",
        hour: "2-digit",
        minute: "2-digit"
      });
}

export function ProjectParamPanel(props: ProjectParamPanelProps) {
  let panelMain: HTMLElement | undefined;
  const [tab, setTab] = createSignal<"settings" | "history">("settings");
  const [key, setKey] = createSignal("");
  const [value, setValue] = createSignal("");
  const [originalValue, setOriginalValue] = createSignal("");
  const [description, setDescription] = createSignal("");
  const [contentType, setContentType] = createSignal<ConfigEntry["contentType"]>("text");
  const [scope, setScope] = createSignal<ConfigEntry["scope"]>("all");
  const [unit, setUnit] = createSignal("");
  const [baseline, setBaseline] = createSignal("");
  const [keyTouched, setKeyTouched] = createSignal(false);
  const [valueTouched, setValueTouched] = createSignal(false);
  const [valueEdited, setValueEdited] = createSignal(false);
  const [status, setStatus] = createSignal("");

  const resetFromEntry = (entry = props.entry) => {
    const isCreate = props.mode === "create";
    const nextKey = isCreate ? "" : (entry?.key ?? "");
    const nextType = isCreate ? "text" : (entry?.contentType ?? "text");
    const rawValue = isCreate ? "" : (entry?.value ?? "");
    const nextValue = nextType === "json" ? formatJson(rawValue) : rawValue;
    const nextDescription = isCreate
      ? ""
      : (entry?.description ?? props.initialDescription ?? "");
    const nextScope = isCreate ? "all" : (entry?.scope ?? "all");
    const nextUnit = isCreate ? "" : (entry?.unit ?? "");

    setKey(nextKey);
    setValue(nextValue);
    setOriginalValue(rawValue);
    setDescription(nextDescription);
    setContentType(nextType);
    setScope(nextScope);
    setUnit(nextUnit);
    setBaseline(JSON.stringify({
      key: nextKey,
      value: nextValue,
      description: nextDescription,
      contentType: nextType,
      scope: nextScope,
      unit: nextUnit
    }));
    setTab("settings");
    setKeyTouched(false);
    setValueTouched(false);
    setValueEdited(false);
    setStatus("");
    requestAnimationFrame(() => panelMain?.scrollTo({ top: 0, left: 0 }));
  };

  createEffect(on(
    () => `${props.open}:${props.mode}:${props.entry?.key ?? ""}`,
    () => {
      if (props.open) resetFromEntry();
    }
  ));

  const currentSnapshot = () => JSON.stringify({
    key: key(),
    value: value(),
    description: description(),
    contentType: contentType(),
    scope: scope(),
    unit: unit()
  });
  const isReadOnly = () => props.mode === "snapshot" || !props.canManage;
  const isDirty = createMemo(() =>
    props.open
    && !isReadOnly()
    && (props.mode === "create" || !!props.entry)
    && currentSnapshot() !== baseline()
  );

  createEffect(() => props.onDirtyChange(isDirty()));

  const validation = createMemo(() => validateConfigEntryDraft({
    // Existing legacy keys remain editable even when they predate hierarchy validation.
    key: props.mode === "create" ? key() : "legacy",
    value: value(),
    contentType: contentType(),
    existingKeys: props.mode === "create"
      ? props.existingEntries.map(entry => entry.key)
      : props.existingEntries.filter(entry => entry.key !== props.entry?.key).map(entry => entry.key)
  }));
  const valueError = createMemo(() => getConfigEntryValueError(contentType(), value()));
  const canSave = () =>
    !isReadOnly() && isDirty() && validation().isValid && !props.isSaving;

  const save = async () => {
    setKeyTouched(true);
    setValueTouched(true);
    if (!canSave()) return;
    setStatus(props.mode === "create" ? "Creating parameter…" : "Saving parameter…");

    try {
      const saved = await props.onSave({
        key: key().trim(),
        value: valueEdited() ? value() : originalValue(),
        description: description().trim(),
        contentType: contentType(),
        scope: scope(),
        unit: contentType() === "number" ? unit().trim() || null : null
      });
      if (saved) resetFromEntry(saved);
      else setBaseline(currentSnapshot());
      setStatus(props.mode === "create" ? "Parameter created." : "Changes saved.");
    } catch (caught) {
      setStatus(caught instanceof Error ? caught.message : "The parameter could not be saved.");
    }
  };

  const openHistory = () => {
    setTab("history");
    const selectedKey = props.entry?.key;
    if (selectedKey) props.onHistoryOpen(selectedKey);
  };

  const historyActionLabel = () =>
    props.mode === "amend" ? "Use in draft" : "Restore";

  const runHistoryAction = async (version: ConfigEntryVersion) => {
    try {
      const restored = await props.onHistoryAction(version);
      if (restored) resetFromEntry(restored);
      setStatus(props.mode === "amend" ? `Version ${version.version} copied into the draft.` : `Version ${version.version} restored as a new version.`);
    } catch (caught) {
      setStatus(caught instanceof Error ? caught.message : "The history action could not be completed.");
    }
  };

  return (
    <Show when={props.open}>
    <Dialog
      open={props.open}
      modal
      preventScroll={false}
      onOpenChange={open => {
        if (!open) props.onRequestClose();
      }}
    >
      <Dialog.Portal>
        <Dialog.Overlay
          data-testid="parameter-panel-overlay"
          class="fixed inset-0 z-60 bg-black/30 backdrop-blur-[1px]"
          onClick={() => props.onRequestClose()}
        />
        <Dialog.Content
          data-testid="parameter-side-panel"
          data-mode={props.mode}
          data-entry-key={props.entry?.key ?? ""}
          onCloseAutoFocus={event => event.preventDefault()}
          class="bg-surface-container-low border-outline-variant/20 fixed inset-0 z-70 flex min-w-0 flex-col border shadow-2xl outline-none md:inset-y-0 md:right-0 md:left-auto md:w-[min(46rem,52vw)] md:border-y-0 md:border-r-0 md:border-l"
        >
          <header class="border-outline-variant/15 grid shrink-0 grid-cols-[auto_minmax(0,1fr)_auto] items-start gap-2 border-b px-4 py-4 sm:gap-4 sm:px-6">
            <button
              type="button"
              data-testid="parameter-panel-back-button"
              onClick={() => props.onRequestClose()}
              aria-label="Back to parameter list"
              class="text-outline hover:bg-surface-container-high hover:text-on-surface inline-flex h-9 w-9 shrink-0 cursor-pointer items-center justify-center rounded-lg border-0 bg-transparent md:hidden"
            >
              <MIcon name="arrow_back" class="text-[20px]" />
            </button>
            <div class="min-w-0">
              <Dialog.Title class="font-headline text-on-surface truncate text-lg font-bold">
                {props.mode === "create" ? "New parameter" : parameterName(props.entry?.key ?? "Parameter")}
              </Dialog.Title>
              <Dialog.Description class="text-outline mt-1 truncate font-mono text-[12px]">
                {props.mode === "create" ? "Create a parameter" : props.entry?.key}
              </Dialog.Description>
            </div>
            <div class="flex shrink-0 items-center gap-2">
              <Show when={props.mode !== "create" && props.entry}>
                {entry => (
                  <span title={props.shareEnabled ? undefined : props.shareDisabledReason}>
                    <button
                      type="button"
                      data-testid="parameter-panel-share-button"
                      disabled={!props.shareEnabled}
                      onClick={() => props.onShare(entry())}
                      aria-label="Share parameter"
                      aria-describedby={!props.shareEnabled ? "parameter-share-disabled-reason" : undefined}
                      class="bg-surface-container-high text-on-surface hover:bg-surface-bright inline-flex h-9 cursor-pointer items-center gap-1.5 rounded-lg border-0 px-3 text-[13px] font-semibold disabled:cursor-not-allowed disabled:opacity-45"
                    >
                      <MIcon name="share" class="text-[17px]" />
                      Share
                    </button>
                  </span>
                )}
              </Show>
              <button
                type="button"
                data-testid="parameter-panel-close-button"
                onClick={() => props.onRequestClose()}
                aria-label="Close parameter panel"
                title="Close parameter panel"
                class="text-outline hover:bg-surface-container-high hover:text-on-surface inline-flex h-9 w-9 cursor-pointer items-center justify-center rounded-lg border-0 bg-transparent"
              >
                <MIcon name="close" class="text-[20px]" />
              </button>
            </div>
          </header>

          <Show when={!props.shareEnabled && props.shareDisabledReason && props.mode !== "create"}>
            <p id="parameter-share-disabled-reason" class="text-outline px-4 pt-2 text-right text-[11px] sm:px-6">
              {props.shareDisabledReason}
            </p>
          </Show>

          <div class="border-outline-variant/15 flex shrink-0 gap-1 border-b px-4 pt-3 sm:px-6" role="tablist">
            <button
              type="button"
              role="tab"
              aria-selected={tab() === "settings"}
              data-testid="parameter-panel-settings-tab"
              onClick={() => setTab("settings")}
              class={`cursor-pointer border-0 border-b-2 bg-transparent px-3 py-2 text-[13px] font-semibold ${tab() === "settings" ? "border-primary text-primary" : "border-transparent text-outline"}`}
            >
              Settings
            </button>
            <Show when={props.mode !== "create"}>
              <button
                type="button"
                role="tab"
                aria-selected={tab() === "history"}
                data-testid="parameter-panel-history-tab"
                onClick={openHistory}
                class={`cursor-pointer border-0 border-b-2 bg-transparent px-3 py-2 text-[13px] font-semibold ${tab() === "history" ? "border-primary text-primary" : "border-transparent text-outline"}`}
              >
                History
              </button>
            </Show>
          </div>

          <main
            ref={element => {
              panelMain = element;
              element.scrollTo({ top: 0, left: 0 });
            }}
            class="min-h-0 flex-1 overflow-y-auto px-4 py-5 sm:px-6"
          >
            <Show when={tab() === "settings"}>
              <div data-testid="parameter-panel-settings" class="space-y-5">
                <div class="bg-surface-container-high/45 border-outline-variant/15 grid grid-cols-2 gap-x-4 gap-y-3 rounded-xl border p-4 text-[12px] sm:grid-cols-4">
                  <div>
                    <span class="text-outline block uppercase">Environment</span>
                    <span class="text-on-surface mt-1 block font-mono">{props.environmentName}</span>
                  </div>
                  <div>
                    <span class="text-outline block uppercase">Datatype</span>
                    <span class="text-on-surface mt-1 block font-mono">{contentType()}</span>
                  </div>
                  <div>
                    <span class="text-outline block uppercase">Scope</span>
                    <span class="text-on-surface mt-1 block font-mono">{scope()}</span>
                  </div>
                  <div>
                    <span class="text-outline block uppercase">Version</span>
                    <span class="text-on-surface mt-1 block font-mono">
                      {props.releaseVersion ? `release ${props.releaseVersion}` : props.entry ? `v${props.entry.activeVersion}` : "new"}
                    </span>
                  </div>
                </div>

                <Show when={props.mode === "create"}>
                  <div>
                    <Label for="parameter-panel-key-input">Full key</Label>
                    <Input
                      id="parameter-panel-key-input"
                      data-testid="parameter-key-input"
                      value={key()}
                      onKeyDown={event => {
                        if (isDisallowedConfigEntryKeyPress(event)) {
                          event.preventDefault();
                        }
                      }}
                      onInput={event => {
                        setKey(readConfigEntryKeyInput(event.currentTarget));
                        setKeyTouched(true);
                      }}
                      onBlur={() => setKeyTouched(true)}
                      aria-invalid={keyTouched() && !!validation().keyError}
                      aria-describedby={keyTouched() && validation().keyError ? "parameter-panel-key-error" : undefined}
                      placeholder="Checkout:FreeShippingThreshold"
                      class="font-mono"
                    />
                    <Show when={keyTouched() && validation().keyError}>
                      <p id="parameter-panel-key-error" role="alert" class="text-error mt-1.5 text-[12px]">
                        {validation().keyError}
                      </p>
                    </Show>
                  </div>
                </Show>

                <Show when={props.mode !== "create"}>
                  <div class="grid gap-3 sm:grid-cols-2">
                    <div>
                      <Label>Parameter name</Label>
                      <div class="bg-surface-container-lowest border-outline-variant/20 text-on-surface rounded-lg border px-3 py-2.5 text-[13px] font-semibold">
                        {parameterName(props.entry?.key ?? "")}
                      </div>
                    </div>
                    <div>
                      <Label>Full key</Label>
                      <div class="bg-surface-container-lowest border-outline-variant/20 text-on-surface truncate rounded-lg border px-3 py-2.5 font-mono text-[13px]" title={props.entry?.key}>
                        {props.entry?.key}
                      </div>
                    </div>
                  </div>
                </Show>

                <div>
                  <Label for="parameter-panel-description-input">Description</Label>
                  <textarea
                    id="parameter-panel-description-input"
                    data-testid="parameter-edit-description-input"
                    value={description()}
                    disabled={isReadOnly()}
                    onInput={event => setDescription(event.currentTarget.value)}
                    rows={4}
                    maxLength={500}
                    placeholder="Explain what this parameter controls…"
                    class="bg-surface-container-lowest border-outline-variant/20 focus:border-primary text-on-surface disabled:text-outline w-full resize-y rounded-xl border px-3 py-2.5 text-[14px] outline-none disabled:cursor-not-allowed"
                  />
                  <p class="text-outline mt-1 text-right text-[11px]">{description().length}/500</p>
                </div>

                <div class="grid gap-4 sm:grid-cols-2">
                  <div>
                    <Label for="parameter-panel-datatype">Datatype</Label>
                    <Select
                      id="parameter-panel-datatype"
                      aria-label="Datatype"
                      value={contentType()}
                      disabled={isReadOnly()}
                      onChange={next => {
                        const selected = next as ConfigEntry["contentType"];
                        setContentType(selected);
                        if (selected !== "number") setUnit("");
                        if (selected === "boolean" && value() !== "true" && value() !== "false") {
                          setValue("false");
                        }
                        setValueTouched(true);
                        setValueEdited(true);
                      }}
                      options={["text", "number", "boolean", "json"]}
                    />
                  </div>
                  <div>
                    <Label for="parameter-panel-scope">Scope</Label>
                    <Select
                      id="parameter-panel-scope"
                      aria-label="Scope"
                      value={scope()}
                      disabled={isReadOnly()}
                      onChange={next => setScope(next as ConfigEntry["scope"])}
                      options={[
                        { value: "all", label: "All" },
                        { value: "client", label: "Client" },
                        { value: "server", label: "Server" }
                      ]}
                    />
                  </div>
                </div>

                <Show when={contentType() === "number"}>
                  <div>
                    <Label for="parameter-panel-unit-input">Unit (optional)</Label>
                    <Input
                      id="parameter-panel-unit-input"
                      data-testid="parameter-edit-unit-input"
                      value={unit()}
                      disabled={isReadOnly()}
                      maxLength={32}
                      onInput={event => setUnit(event.currentTarget.value)}
                      placeholder="ms, %, USD…"
                    />
                  </div>
                </Show>

                <div>
                  <Label>Value</Label>
                  <Show
                    when={contentType() === "json"}
                    fallback={
                      <div class="bg-surface-container-lowest border-outline-variant/15 rounded-xl border p-3">
                        <ParameterValueEditor
                          entry={{
                            project: props.projectId,
                            environment: props.environmentName,
                            key: key() || "new-parameter",
                            value: value(),
                            contentType: contentType(),
                            scope: scope(),
                            unit: unit() || null,
                            activeVersion: props.entry?.activeVersion ?? 0,
                            createdAt: props.entry?.createdAt ?? "",
                            updatedAt: props.entry?.updatedAt ?? ""
                          }}
                          value={value()}
                          onChange={next => {
                            setValue(next);
                            setValueTouched(true);
                            setValueEdited(true);
                          }}
                          readOnly={isReadOnly()}
                          invalid={valueTouched() && !!valueError()}
                          describedBy={valueTouched() && valueError() ? "parameter-panel-value-error" : undefined}
                          testId={props.mode === "create" ? "parameter-value-input" : "parameter-edit-value-input"}
                        />
                      </div>
                    }
                  >
                    <Show
                      when={!isReadOnly()}
                      fallback={
                        <pre class="bg-surface-container-lowest border-outline-variant/20 text-on-surface min-h-48 overflow-auto rounded-xl border p-4 font-mono text-[13px] whitespace-pre-wrap">
                          {value()}
                        </pre>
                      }
                    >
                      <VisualJsonEditor
                        id="parameter-panel-json-editor"
                        aria-label="JSON value"
                        aria-invalid={valueTouched() && !!valueError()}
                        aria-describedby={valueTouched() && valueError() ? "parameter-panel-value-error" : undefined}
                        value={value()}
                        onChange={next => {
                          if (next !== value()) {
                            setValueTouched(true);
                            setValueEdited(true);
                          }
                          setValue(next);
                        }}
                      />
                    </Show>
                  </Show>
                  <Show when={valueTouched() && valueError()}>
                    <p id="parameter-panel-value-error" role="alert" aria-live="polite" class="text-error mt-2 text-[12px]">
                      {valueError()}
                    </p>
                  </Show>
                </div>
              </div>
            </Show>

            <Show when={tab() === "history"}>
              <section data-testid="parameter-panel-history" class="space-y-3">
                <Show when={props.mode === "amend"}>
                  <p class="text-on-surface-variant text-[13px]">
                    Select a live version to copy its complete value and metadata into this release draft.
                  </p>
                </Show>
                <Show when={props.mode === "snapshot"}>
                  <p class="text-on-surface-variant text-[13px]">
                    The release value is marked below when it matches a live version. History is read-only.
                  </p>
                </Show>
                <Show
                  when={!props.isHistoryLoading}
                  fallback={<div class="skeleton h-36 w-full rounded-xl" />}
                >
                  <Show
                    when={props.historyVersions.length > 0}
                    fallback={<p class="text-outline py-12 text-center text-[13px]">No version history.</p>}
                  >
                    <div class="space-y-2">
                      <For each={props.historyVersions}>
                        {version => {
                          const isActive = () => props.mode === "live"
                            && version.version === props.entry?.activeVersion;
                          const isCaptured = () => props.mode === "snapshot"
                            && version.value === props.entry?.value
                            && version.contentType === props.entry?.contentType
                            && version.scope === props.entry?.scope
                            && (version.description ?? null) === (props.entry?.description ?? null)
                            && (version.unit ?? null) === (props.entry?.unit ?? null);

                          return (
                            <article
                              data-testid={`parameter-history-version-${version.version}`}
                              class="bg-surface-container border-outline-variant/15 rounded-xl border p-4"
                            >
                              <div class="flex items-start justify-between gap-3">
                                <div class="min-w-0">
                                  <div class="flex flex-wrap items-center gap-2">
                                    <span class="text-on-surface font-mono text-[14px] font-bold">v{version.version}</span>
                                    <Show when={isActive()}>
                                      <span class="text-secondary text-[11px] font-bold uppercase">In use</span>
                                    </Show>
                                    <Show when={isCaptured()}>
                                      <span class="text-primary text-[11px] font-bold uppercase">Captured in release</span>
                                    </Show>
                                  </div>
                                  <p class="text-on-surface-variant mt-1 text-[12px]">
                                    Changed by {version.actor} · {formatDate(version.createdAt)}
                                  </p>
                                </div>
                                <Show when={props.mode !== "snapshot" && !isActive() && props.canManage}>
                                  <Button
                                    type="button"
                                    variant="secondary"
                                    size="sm"
                                    disabled={props.isHistoryActionPending}
                                    onClick={() => void runHistoryAction(version)}
                                  >
                                    <MIcon name="history" class="text-[15px]" />
                                    {historyActionLabel()}
                                  </Button>
                                </Show>
                              </div>
                              <pre class="bg-surface-container-lowest text-on-surface mt-3 max-h-40 overflow-auto rounded-lg p-3 font-mono text-[12px] whitespace-pre-wrap">
                                {version.contentType === "json" ? formatJson(version.value) : version.value}
                                <Show when={version.contentType === "number" && version.unit}> {version.unit}</Show>
                              </pre>
                              <div class="text-outline mt-2 flex gap-3 text-[11px] uppercase">
                                <span>Datatype {version.contentType}</span>
                                <span>Scope {version.scope}</span>
                              </div>
                            </article>
                          );
                        }}
                      </For>
                    </div>
                  </Show>
                </Show>
              </section>
            </Show>
          </main>

          <footer class="bg-surface-container-low border-outline-variant/15 sticky bottom-0 flex shrink-0 items-center justify-between gap-3 border-t px-4 py-3 sm:px-6">
            <p role="status" aria-live="polite" class="text-on-surface-variant min-w-0 text-[12px]">
              {status()}
            </p>
            <div class="flex shrink-0 items-center gap-2">
              <Button type="button" variant="outline" onClick={() => props.onRequestClose()}>
                {props.mode === "snapshot" ? "Back" : "Cancel"}
              </Button>
              <Show when={!isReadOnly()}>
                <Button
                  type="button"
                  data-testid={props.mode === "create" ? "parameter-create-submit-button" : "parameter-edit-save-button"}
                  disabled={!canSave()}
                  onClick={() => void save()}
                >
                  {props.isSaving ? "Saving…" : props.mode === "create" ? "Create" : "Save"}
                </Button>
              </Show>
            </div>
          </footer>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog>
    </Show>
  );
}
