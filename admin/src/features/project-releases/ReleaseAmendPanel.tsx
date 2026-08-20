import { makePersisted } from "@solid-primitives/storage";
import { createStore } from "solid-js/store";
import { Show, createEffect, createMemo, createSignal } from "solid-js";
import { Button } from "../../shared/ui/button";
import { MIcon } from "../../shared/ui/icons";
import { Input } from "../../shared/ui/input";
import { Label } from "../../shared/ui/label";
import { Select } from "../../shared/ui/select";
import { Tooltip, TooltipLabel, TooltipTrigger } from "../../shared/ui/tooltip";
import { tooltipCopy } from "../../shared/lib/tooltip-copy";
import { useUnsavedChangesBlocker } from "../../shared/hooks/useUnsavedChanges";
import type { ConfigEntry, ConfigReleaseEntry } from "../../types";
import {
  type ConfigEntryContentType,
  validateConfigEntryDraft,
} from "../project-param-edit/config-entry-value";
import {
  PARAMETER_DENSITY_STORAGE_KEY
} from "../project-params/ProjectParamsTab";
import {
  ProjectParamsTable,
  type ParameterViewDensity
} from "../project-params/ProjectParamsTable";

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
        entry.scope === right[index]?.scope &&
        entry.description === right[index]?.description &&
        entry.unit === right[index]?.unit
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
  const [editingEntry, setEditingEntry] = createSignal<ConfigEntry | null>(null);
  const [density, setDensity] = makePersisted(
    // eslint-disable-next-line solid/reactivity -- makePersisted intentionally wraps the signal.
    createSignal<ParameterViewDensity>("compact"),
    {
      name: PARAMETER_DENSITY_STORAGE_KEY,
      deserialize: value => {
        try {
          const parsed = JSON.parse(value) as unknown;
          return parsed === "comfortable" || parsed === "compact" ? parsed : "compact";
        } catch {
          return "compact";
        }
      }
    }
  );

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

  const normalizedRows = createMemo<ConfigEntry[]>(() => rows.map(row => ({
    project: props.projectId,
    environment: bufferEnvironmentName() || props.environmentName,
    key: row.key,
    value: row.value,
    contentType:
      row.contentType === "number" || row.contentType === "boolean" || row.contentType === "json"
        ? row.contentType
        : "text",
    scope: row.scope === "client" || row.scope === "server" ? row.scope : "all",
    description: row.description,
    unit: row.unit,
    activeVersion: 1,
    createdAt: "",
    updatedAt: ""
  })));

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
                onInput={e => {
                  setNewKey(e.currentTarget.value);
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
            <div class="flex justify-end">
              <div
                role="group"
                aria-label="Parameter spacing"
                class="border-outline-variant/20 bg-surface-container-high flex items-center rounded-lg border p-1"
              >
                <button
                  type="button"
                  aria-label="Comfortable spacing"
                  aria-pressed={density() === "comfortable"}
                  title="Comfortable spacing"
                  onClick={() => setDensity("comfortable")}
                  class={`inline-flex h-8 w-8 cursor-pointer items-center justify-center rounded-md border-0 ${density() === "comfortable" ? "bg-surface-container-lowest text-primary" : "text-outline bg-transparent"}`}
                >
                  <MIcon name="density_medium" class="text-[17px]" />
                </button>
                <button
                  type="button"
                  aria-label="Compact spacing"
                  aria-pressed={density() === "compact"}
                  title="Compact spacing"
                  onClick={() => setDensity("compact")}
                  class={`inline-flex h-8 w-8 cursor-pointer items-center justify-center rounded-md border-0 ${density() === "compact" ? "bg-surface-container-lowest text-primary" : "text-outline bg-transparent"}`}
                >
                  <MIcon name="density_small" class="text-[17px]" />
                </button>
              </div>
            </div>
            <ProjectParamsTable
              isLoading={false}
              projectId={props.projectId}
              activeEnvName={bufferEnvironmentName() || props.environmentName}
              filteredConfig={normalizedRows()}
              editingEntry={editingEntry()}
              onSelectEntry={entry => setEditingEntry(entry)}
              onShareEntry={() => undefined}
              onDeleteEntry={key => {
                const index = rows.findIndex(row => row.key === key);
                if (index >= 0) removeRow(index);
              }}
              onUpdateValue={(entry, value) => {
                const index = rows.findIndex(row => row.key === entry.key);
                if (index >= 0) updateRow(index, { value });
              }}
              canManage
              copiedKey={null}
              onCopyValue={() => undefined}
              getParamMeta={() => ({ displayName: "", description: "" })}
              initialDescription={editingEntry()?.description ?? ""}
              onCloseEntry={() => setEditingEntry(null)}
              onEditDirtyChange={() => undefined}
              onSaveSettings={data => {
                const selected = editingEntry();
                const index = selected ? rows.findIndex(row => row.key === selected.key) : -1;
                if (index < 0) return;
                const patch = {
                  value: data.value,
                  contentType: data.contentType,
                  scope: data.scope,
                  description: data.description,
                  unit: data.contentType === "number" ? data.unit : null
                };
                updateRow(index, patch);
                setEditingEntry(current => current ? { ...current, ...patch } : current);
              }}
              isSaving={false}
              historyVersions={[]}
              isHistoryLoading={false}
              isRollingBack={false}
              onRollbackVersion={() => undefined}
              search=""
              density={density()}
            />
          </div>
        </Show>
      </Show>
    </section>
  );
}
