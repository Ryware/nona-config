import { createStore } from "solid-js/store";
import { For, Show, createEffect, createMemo, createSignal } from "solid-js";
import { Button } from "../../shared/ui/button";
import { MIcon } from "../../shared/ui/icons";
import { Input } from "../../shared/ui/input";
import { Label } from "../../shared/ui/label";
import { Select } from "../../shared/ui/select";
import { Tooltip, TooltipLabel, TooltipTrigger } from "../../shared/ui/tooltip";
import { tooltipCopy } from "../../shared/lib/tooltip-copy";
import { useUnsavedChangesBlocker } from "../../shared/hooks/useUnsavedChanges";
import type { ConfigReleaseEntry } from "../../types";
import {
  type ConfigEntryContentType,
  isDisallowedConfigEntryKeyPress,
  readConfigEntryKeyInput,
  validateConfigEntryDraft,
} from "../project-param-edit/config-entry-value";

const TYPE_OPTIONS = [
  { value: "text", label: "Text" },
  { value: "number", label: "Number" },
  { value: "boolean", label: "Boolean" },
  { value: "json", label: "JSON" }
];

const SCOPE_OPTIONS = [
  { value: "client", label: "Client" },
  { value: "server", label: "Server" },
  { value: "all", label: "All" }
];

const RELEASE_AMEND_DRAFT = "release-amend-draft";

function entriesMatch(left: ConfigReleaseEntry[], right: ConfigReleaseEntry[]) {
  return (
    left.length === right.length &&
    left.every(
      (entry, index) =>
        entry.key === right[index]?.key &&
        entry.value === right[index]?.value &&
        entry.contentType === right[index]?.contentType &&
        entry.scope === right[index]?.scope
    )
  );
}

interface ReleaseAmendPanelProps {
  projectId: string;
  environmentName: string;
  sourceVersion: string;
  targetVersion: string;
  sourceEntries: ConfigReleaseEntry[];
  isLoading: boolean;
  isPublishing: boolean;
  onPublish: (environmentName: string, entries: ConfigReleaseEntry[]) => void;
  onCancel: () => void;
}

/**
 * Edits a client-side copy of a release's parameters to publish as a new patch.
 * The environment's working configuration is never touched — publishing sends
 * this buffer to the server as an explicit payload.
 */
export function ReleaseAmendPanel(props: ReleaseAmendPanelProps) {
  const [rows, setRows] = createStore<ConfigReleaseEntry[]>([]);
  const [seededIdentity, setSeededIdentity] = createSignal<string | null>(null);
  const [bufferEnvironmentName, setBufferEnvironmentName] = createSignal("");
  const [newKey, setNewKey] = createSignal("");
  const [newValue, setNewValue] = createSignal("");
  const [newType, setNewType] = createSignal<ConfigEntryContentType>("text");
  const [newScope, setNewScope] = createSignal("all");
  const [keyTouched, setKeyTouched] = createSignal(false);
  const [valueTouched, setValueTouched] = createSignal(false);

  const keyErrorId = "amend-new-key-error";
  const valueErrorId = "amend-new-value-error";
  const actionStatusId = "amend-add-status";

  const currentIdentity = () =>
    JSON.stringify([props.projectId, props.environmentName, props.sourceVersion]);

  const resetAddRowState = () => {
    setNewKey("");
    setNewValue("");
    setNewType("text");
    setNewScope("all");
    setKeyTouched(false);
    setValueTouched(false);
  };

  const isBufferReady = () => !props.isLoading && seededIdentity() === currentIdentity();

  const addValidation = createMemo(() =>
    validateConfigEntryDraft({
      key: newKey(),
      value: newValue(),
      contentType: newType(),
      existingKeys: rows.map((row) => row.key),
    }),
  );

  const keyError = () => (keyTouched() ? addValidation().keyError : undefined);
  const valueError = () => (valueTouched() ? addValidation().valueError : undefined);
  const hasVisibleFieldError = () => !!keyError() || !!valueError();
  const actionStatus = () =>
    addValidation().disabledReason ?? "Parameter is ready to add to this release.";

  const isDirty = createMemo(
    () =>
      isBufferReady() &&
      (!entriesMatch(rows, props.sourceEntries) ||
        newKey() !== "" ||
        newValue() !== "" ||
        newType() !== "text" ||
        newScope() !== "all")
  );

  const discardDraft = () => {
    setRows(props.sourceEntries.map(entry => ({ ...entry })));
    resetAddRowState();
  };

  useUnsavedChangesBlocker({
    id: RELEASE_AMEND_DRAFT,
    isDirty,
    discard: discardDraft
  });

  createEffect(() => {
    const identity = currentIdentity();
    if (seededIdentity() === identity) return;

    setRows([]);
    setBufferEnvironmentName("");
    resetAddRowState();

    if (props.isLoading) return;

    setRows(props.sourceEntries.map(entry => ({ ...entry })));
    setBufferEnvironmentName(props.environmentName);
    setSeededIdentity(identity);
  });

  const updateRow = (index: number, patch: Partial<ConfigReleaseEntry>) =>
    setRows(index, row => ({ ...row, ...patch }));

  const removeRow = (index: number) => {
    const nextRows = rows.filter((_, rowIndex) => rowIndex !== index);
    setRows(nextRows);
  };

  const addRow = () => {
    setKeyTouched(true);
    setValueTouched(true);
    if (!addValidation().isValid) return;

    const key = newKey().trim();
    setRows(currentRows => [
      ...currentRows,
      { key, value: newValue(), contentType: newType(), scope: newScope() }
    ]);
    setNewKey("");
    setNewValue("");
    setNewType("text");
    setNewScope("all");
    setKeyTouched(false);
    setValueTouched(false);
  };

  return (
    <section
      data-testid="release-amend-panel"
      class="bg-surface-container-low border-outline-variant/15 space-y-4 rounded-2xl border p-5"
    >
      <div class="border-primary/25 bg-primary/5 flex flex-col gap-3 rounded-xl border p-4 sm:flex-row sm:items-center sm:justify-between">
        <div class="flex items-center gap-2 text-[13px]">
          <MIcon name="edit" class="text-primary text-[18px]" />
          <span class="text-on-surface-variant">
            Amending <span class="text-on-surface font-mono font-bold">{props.sourceVersion}</span> →
            creating patch{" "}
            <span class="text-primary font-mono font-bold">{props.targetVersion}</span>.
          </span>
        </div>
        <div class="flex shrink-0 flex-wrap justify-end gap-2">
          <Button
            data-testid="release-amend-confirm-button"
            type="button"
            disabled={props.isPublishing || !isBufferReady()}
            onClick={() => {
              const environmentName = bufferEnvironmentName();
              if (!environmentName || !isBufferReady()) return;

              props.onPublish(environmentName, rows.map(entry => ({ ...entry })));
            }}
          >
            <MIcon name="check" class="text-[16px]" />
            {props.isPublishing ? "Creating…" : "Create release"}
          </Button>
          <Button
            data-testid="release-amend-cancel-button"
            type="button"
            variant="outline"
            disabled={props.isPublishing}
            onClick={() => props.onCancel()}
          >
            <MIcon name="close" class="text-[16px]" />
            Cancel
          </Button>
        </div>
      </div>

      <Show
        when={isBufferReady()}
        fallback={<div class="skeleton h-40 w-full rounded-xl" />}
      >
        <div class="bg-surface-container border-outline-variant/15 space-y-3 rounded-xl border p-4">
          <p class="text-outline font-headline text-[10px] font-bold tracking-widest uppercase">
            Add parameter
          </p>
          <div class="grid gap-2 md:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_auto_auto_auto] md:items-start">
            <div>
              <Label for="amend-new-key">Key</Label>
              <Input
                id="amend-new-key"
                data-testid="amend-new-key"
                value={newKey()}
                onKeyDown={e => {
                  if (isDisallowedConfigEntryKeyPress(e)) {
                    e.preventDefault();
                  }
                }}
                onInput={e => {
                  setNewKey(readConfigEntryKeyInput(e.currentTarget));
                  setKeyTouched(true);
                }}
                onBlur={() => setKeyTouched(true)}
                aria-invalid={!!keyError()}
                aria-describedby={keyError() ? keyErrorId : undefined}
                placeholder="Features:Checkout"
                class="h-10 font-mono"
              />
              <Show when={keyError()}>
                <p id={keyErrorId} class="text-error mt-1.5 text-[11px] font-bold">
                  {keyError()}
                </p>
              </Show>
            </div>
            <div>
              <Label for="amend-new-value">Value</Label>
              <Input
                id="amend-new-value"
                data-testid="amend-new-value"
                value={newValue()}
                onInput={e => {
                  setNewValue(e.currentTarget.value);
                  setValueTouched(true);
                }}
                onBlur={() => setValueTouched(true)}
                aria-invalid={!!valueError()}
                aria-describedby={valueError() ? valueErrorId : undefined}
                placeholder="value"
                class="h-10 font-mono"
              />
              <Show when={valueError()}>
                <p id={valueErrorId} class="text-error mt-1.5 text-[11px] font-bold">
                  {valueError()}
                </p>
              </Show>
            </div>
            <div class="w-full md:w-28">
              <TooltipLabel for="amend-new-type" content={tooltipCopy.datatype}>Type</TooltipLabel>
              <Select
                id="amend-new-type"
                aria-label="Type"
                value={newType()}
                onChange={(value) => {
                  setNewType(value as ConfigEntryContentType);
                  setNewValue("");
                  setValueTouched(true);
                }}
                options={TYPE_OPTIONS}
                class="h-10"
              />
            </div>
            <div class="w-full md:w-28">
              <TooltipLabel for="amend-new-scope" content={tooltipCopy.scope}>Scope</TooltipLabel>
              <Select
                id="amend-new-scope"
                aria-label="Scope"
                value={newScope()}
                onChange={setNewScope}
                options={SCOPE_OPTIONS}
                class="h-10"
              />
            </div>
            <div>
              <span
                aria-hidden="true"
                class="text-on-surface-variant invisible mb-1.5 hidden text-[11px] font-medium tracking-[0.05em] md:block"
              >
                Action
              </span>
              <Show
                when={!addValidation().isValid}
                fallback={
                  <Button
                    data-testid="amend-add-button"
                    type="button"
                    variant="secondary"
                    onClick={addRow}
                  >
                    <MIcon name="add" class="text-[16px]" />
                    Add
                  </Button>
                }
              >
                <Tooltip content={actionStatus()}>
                  <TooltipTrigger
                    as="span"
                    tabindex="0"
                    data-tooltip-trigger
                    class="inline-flex rounded-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
                  >
                    <Button
                      data-testid="amend-add-button"
                      type="button"
                      variant="secondary"
                      disabled
                      aria-describedby={actionStatusId}
                    >
                      <MIcon name="add" class="text-[16px]" />
                      Add
                    </Button>
                  </TooltipTrigger>
                </Tooltip>
              </Show>
            </div>
          </div>
          <p
            id={actionStatusId}
            role="status"
            aria-live="polite"
            class={
              hasVisibleFieldError()
                ? "sr-only"
                : "text-on-surface-variant ml-auto w-fit max-w-full text-[11px] md:text-right"
            }
          >
            {actionStatus()}
          </p>
        </div>

        <Show
          when={rows.length > 0}
          fallback={
            <div class="bg-surface-container rounded-xl px-4 py-5 text-center text-xs text-on-surface-variant">
              This release has no parameters.
            </div>
          }
        >
          <div class="space-y-2">
            <For each={rows}>
              {(row, index) => (
                <div
                  data-testid={`amend-row-${row.key}`}
                  class="bg-surface-container grid gap-2 rounded-xl px-4 py-3 md:grid-cols-[minmax(0,1.2fr)_minmax(0,1.5fr)_auto_auto_auto] md:items-center"
                >
                  <span class="text-on-surface truncate font-mono text-[13px] font-bold" title={row.key}>
                    {row.key}
                  </span>
                  <Input
                    data-testid={`amend-value-${row.key}`}
                    value={row.value}
                    onInput={e => updateRow(index(), { value: e.currentTarget.value })}
                    class="h-9 font-mono"
                  />
                  <div class="w-full md:w-28">
                    <Select
                      value={row.contentType}
                      onChange={value => updateRow(index(), { contentType: value })}
                      options={TYPE_OPTIONS}
                      class="h-9"
                    />
                  </div>
                  <div class="w-full md:w-28">
                    <Select
                      value={row.scope}
                      onChange={value => updateRow(index(), { scope: value })}
                      options={SCOPE_OPTIONS}
                      class="h-9"
                    />
                  </div>
                  <button
                    type="button"
                    onClick={() => removeRow(index())}
                    aria-label={`Remove ${row.key}`}
                    title={`Remove ${row.key}`}
                    class="bg-error-container/10 text-error hover:bg-error-container/20 inline-flex h-9 w-9 cursor-pointer items-center justify-center rounded-lg border-0"
                  >
                    <MIcon name="delete" class="text-[16px]" />
                  </button>
                </div>
              )}
            </For>
          </div>
        </Show>
      </Show>
    </section>
  );
}
