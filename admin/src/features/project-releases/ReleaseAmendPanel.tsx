import { createStore } from "solid-js/store";
import { For, Show, createEffect, createSignal } from "solid-js";
import { Button } from "../../shared/ui/button";
import { MIcon } from "../../shared/ui/icons";
import { Input } from "../../shared/ui/input";
import { Label } from "../../shared/ui/label";
import { Select } from "../../shared/ui/select";
import type { ConfigReleaseEntry } from "../../types";

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

interface ReleaseAmendPanelProps {
  sourceVersion: string;
  targetVersion: string;
  sourceEntries: ConfigReleaseEntry[];
  isLoading: boolean;
  isPublishing: boolean;
  onPublish: (entries: ConfigReleaseEntry[]) => void;
  onCancel: () => void;
}

/**
 * Edits a client-side copy of a release's parameters to publish as a new patch.
 * The environment's working configuration is never touched — publishing sends
 * this buffer to the server as an explicit payload.
 */
export function ReleaseAmendPanel(props: ReleaseAmendPanelProps) {
  const [rows, setRows] = createStore<ConfigReleaseEntry[]>([]);
  const [ready, setReady] = createSignal(false);
  const [newKey, setNewKey] = createSignal("");
  const [newValue, setNewValue] = createSignal("");
  const [newType, setNewType] = createSignal("text");
  const [newScope, setNewScope] = createSignal("all");
  const [error, setError] = createSignal("");

  // Seed the local buffer once the source release has loaded.
  createEffect(() => {
    if (!props.isLoading && !ready()) {
      setRows(props.sourceEntries.map(entry => ({ ...entry })));
      setReady(true);
    }
  });

  const updateRow = (index: number, patch: Partial<ConfigReleaseEntry>) =>
    setRows(index, row => ({ ...row, ...patch }));

  const removeRow = (index: number) => {
    const nextRows = rows.filter((_, rowIndex) => rowIndex !== index);
    setRows(nextRows);
  };

  const addRow = () => {
    const key = newKey().trim();
    if (!key) {
      setError("Key is required.");
      return;
    }
    if (rows.some(row => row.key.toLowerCase() === key.toLowerCase())) {
      setError("That key already exists.");
      return;
    }
    setRows(currentRows => [
      ...currentRows,
      { key, value: newValue(), contentType: newType(), scope: newScope() }
    ]);
    setNewKey("");
    setNewValue("");
    setNewType("text");
    setNewScope("all");
    setError("");
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
            disabled={props.isPublishing || props.isLoading}
            onClick={() => props.onPublish([...rows])}
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
        when={!props.isLoading}
        fallback={<div class="skeleton h-40 w-full rounded-xl" />}
      >
        <div class="bg-surface-container border-outline-variant/15 space-y-3 rounded-xl border p-4">
          <p class="text-outline font-headline text-[10px] font-bold tracking-widest uppercase">
            Add parameter
          </p>
          <div class="grid gap-2 md:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_auto_auto_auto] md:items-end">
            <div>
              <Label for="amend-new-key">Key</Label>
              <Input
                id="amend-new-key"
                data-testid="amend-new-key"
                value={newKey()}
                onInput={e => {
                  setNewKey(e.currentTarget.value);
                  if (error()) setError("");
                }}
                placeholder="Features:Checkout"
                class="h-10 font-mono"
              />
            </div>
            <div>
              <Label for="amend-new-value">Value</Label>
              <Input
                id="amend-new-value"
                data-testid="amend-new-value"
                value={newValue()}
                onInput={e => setNewValue(e.currentTarget.value)}
                placeholder="value"
                class="h-10 font-mono"
              />
            </div>
            <div class="w-full md:w-28">
              <Label for="amend-new-type">Type</Label>
              <Select value={newType()} onChange={setNewType} options={TYPE_OPTIONS} class="h-10" />
            </div>
            <div class="w-full md:w-28">
              <Label for="amend-new-scope">Scope</Label>
              <Select
                value={newScope()}
                onChange={setNewScope}
                options={SCOPE_OPTIONS}
                class="h-10"
              />
            </div>
            <Button data-testid="amend-add-button" type="button" variant="secondary" onClick={addRow}>
              <MIcon name="add" class="text-[16px]" />
              Add
            </Button>
          </div>
          <Show when={error()}>
            <p class="text-error text-[11px] font-bold">{error()}</p>
          </Show>
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
