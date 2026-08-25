import { useQuery } from "@tanstack/solid-query";
import { createEffect, createMemo, createSignal, onMount, Show } from "solid-js";
import { createStore } from "solid-js/store";
import { configEntryService } from "../../entities/project/api/config-entry.service";
import { useUnsavedChanges, useUnsavedChangesBlocker } from "../../shared/hooks/useUnsavedChanges";
import { Button } from "../../shared/ui/button";
import { ConfirmDialog } from "../../shared/ui/confirm-dialog";
import { MIcon } from "../../shared/ui/icons";
import { Input } from "../../shared/ui/input";
import type { ConfigEntry, ConfigEntryVersion, ConfigReleaseEntry } from "../../types";
import {
  ProjectParamPanel,
  type ParameterPanelSaveData
} from "../project-param-edit/ProjectParamPanel";
import { clearLegacyParameterDensityPreference } from "../project-params/ProjectParamsTab";
import { ProjectParamsTable } from "../project-params/ProjectParamsTable";

const RELEASE_AMEND_DRAFT = "release-amend-draft";
const RELEASE_AMEND_PANEL_DRAFT = "release-amend-panel-draft";

function entriesMatch(left: ConfigReleaseEntry[], right: ConfigReleaseEntry[]) {
  return left.length === right.length && left.every((entry, index) => {
    const candidate = right[index];
    return entry.key === candidate?.key
      && entry.value === candidate.value
      && entry.contentType === candidate.contentType
      && entry.scope === candidate.scope
      && entry.description === candidate.description
      && entry.unit === candidate.unit;
  });
}

function normalizeEntry(
  projectId: string,
  environmentName: string,
  entry: ConfigReleaseEntry
): ConfigEntry {
  return {
    project: projectId,
    environment: environmentName,
    key: entry.key,
    value: entry.value,
    contentType:
      entry.contentType === "number" || entry.contentType === "boolean" || entry.contentType === "json"
        ? entry.contentType
        : "text",
    scope: entry.scope === "client" || entry.scope === "server" ? entry.scope : "all",
    description: entry.description,
    unit: entry.unit,
    activeVersion: 1,
    createdAt: "",
    updatedAt: ""
  };
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

/** Keeps all amendments isolated in a client-side release buffer until publish. */
export function ReleaseAmendPanel(props: ReleaseAmendPanelProps) {
  const [rows, setRows] = createStore<ConfigReleaseEntry[]>([]);
  const [seededIdentity, setSeededIdentity] = createSignal("");
  const [bufferEnvironmentName, setBufferEnvironmentName] = createSignal("");
  const [selectedEntry, setSelectedEntry] = createSignal<ConfigEntry | null>(null);
  const [creating, setCreating] = createSignal(false);
  const [panelDirty, setPanelDirty] = createSignal(false);
  const [historyKey, setHistoryKey] = createSignal("");
  const [deleteKey, setDeleteKey] = createSignal<string | null>(null);
  const [search, setSearch] = createSignal("");
  const { requestAction } = useUnsavedChanges();
  let panelOpener: HTMLElement | undefined;

  onMount(clearLegacyParameterDensityPreference);

  const identity = () => JSON.stringify([props.projectId, props.environmentName, props.sourceVersion]);
  const isBufferReady = () => !props.isLoading && seededIdentity() === identity();
  const normalizedRows = createMemo(() => rows.map(row =>
    normalizeEntry(props.projectId, bufferEnvironmentName() || props.environmentName, row)
  ));
  const filteredRows = createMemo(() => {
    const query = search().trim().toLowerCase();
    if (!query) return normalizedRows();
    return normalizedRows().filter(entry =>
      entry.key.toLowerCase().includes(query)
      || entry.value.toLowerCase().includes(query)
      || (entry.description ?? "").toLowerCase().includes(query)
      || (entry.unit ?? "").toLowerCase().includes(query)
    );
  });
  const isReleaseDirty = createMemo(() =>
    isBufferReady() && !entriesMatch(rows, props.sourceEntries)
  );

  const returnFocus = () => {
    const opener = panelOpener;
    panelOpener = undefined;
    requestAnimationFrame(() => opener?.focus({ preventScroll: true }));
  };

  const closePanel = () => {
    setPanelDirty(false);
    setCreating(false);
    setSelectedEntry(null);
    setHistoryKey("");
    returnFocus();
  };

  const resetRelease = () => {
    setRows(props.sourceEntries.map(entry => ({ ...entry })));
    closePanel();
  };

  useUnsavedChangesBlocker({ id: RELEASE_AMEND_DRAFT, isDirty: isReleaseDirty, discard: resetRelease });
  useUnsavedChangesBlocker({ id: RELEASE_AMEND_PANEL_DRAFT, isDirty: panelDirty, discard: closePanel });

  createEffect(() => {
    const nextIdentity = identity();
    if (seededIdentity() === nextIdentity) return;
    setRows([]);
    setBufferEnvironmentName("");
    setSearch("");
    closePanel();
    if (props.isLoading) return;
    setRows(props.sourceEntries.map(entry => ({ ...entry })));
    setBufferEnvironmentName(props.environmentName);
    setSeededIdentity(nextIdentity);
  });

  const historyQuery = useQuery(() => ({
    queryKey: ["amend-parameter-history", props.projectId, props.environmentName, historyKey()],
    queryFn: () => configEntryService.history(props.projectId, props.environmentName, historyKey()),
    enabled: !!historyKey(),
    staleTime: 60_000
  }));

  const selectEntry = (entry: ConfigEntry, opener?: HTMLElement) => {
    if (selectedEntry()?.key === entry.key && !creating()) return;
    requestAction(() => {
      panelOpener = opener;
      setCreating(false);
      setSelectedEntry(entry);
      setPanelDirty(false);
      setHistoryKey("");
    }, [RELEASE_AMEND_PANEL_DRAFT]);
  };

  const openCreate = (opener: HTMLElement) => {
    requestAction(() => {
      panelOpener = opener;
      setCreating(true);
      setSelectedEntry(null);
      setPanelDirty(false);
      setHistoryKey("");
    }, [RELEASE_AMEND_PANEL_DRAFT]);
  };

  const savePanel = async (data: ParameterPanelSaveData) => {
    const releaseEntry: ConfigReleaseEntry = {
      key: data.key,
      value: data.value,
      contentType: data.contentType,
      scope: data.scope,
      description: data.description,
      unit: data.unit
    };

    if (creating()) {
      setRows(current => [...current, releaseEntry]);
      const created = normalizeEntry(props.projectId, bufferEnvironmentName(), releaseEntry);
      setCreating(false);
      setSelectedEntry(created);
      setPanelDirty(false);
      return created;
    }

    const selected = selectedEntry();
    const index = selected ? rows.findIndex(row => row.key === selected.key) : -1;
    if (index < 0) throw new Error("The draft parameter could not be found.");
    setRows(index, releaseEntry);
    const updated = normalizeEntry(props.projectId, bufferEnvironmentName(), releaseEntry);
    setSelectedEntry(updated);
    setPanelDirty(false);
    return updated;
  };

  const useHistoryVersion = async (version: ConfigEntryVersion) => {
    const selected = selectedEntry();
    const index = selected ? rows.findIndex(row => row.key === selected.key) : -1;
    if (!selected || index < 0) throw new Error("The draft parameter could not be found.");
    const releaseEntry: ConfigReleaseEntry = {
      key: selected.key,
      value: version.value,
      contentType: version.contentType,
      scope: version.scope,
      description: version.description,
      unit: version.unit
    };
    setRows(index, releaseEntry);
    const updated = normalizeEntry(props.projectId, bufferEnvironmentName(), releaseEntry);
    setSelectedEntry(updated);
    return updated;
  };

  return (
    <section data-testid="release-amend-panel" class="bg-surface-container-low border-outline-variant/15 space-y-4 rounded-2xl border p-4 sm:p-5">
      <div class="border-primary/25 bg-primary/5 flex flex-col gap-3 rounded-xl border p-4 sm:flex-row sm:items-center sm:justify-between">
        <div class="flex items-center gap-2 text-[14px]">
          <MIcon name="edit" class="text-primary text-[18px]" />
          <span class="text-on-surface-variant">
            Amending <span class="text-on-surface font-mono font-bold">{props.sourceVersion}</span> → creating patch <span class="text-primary font-mono font-bold">{props.targetVersion}</span>.
          </span>
        </div>
        <div class="flex shrink-0 flex-wrap justify-end gap-2">
          <Button type="button" variant="secondary" onClick={event => openCreate(event.currentTarget)}>
            <MIcon name="add" class="text-[16px]" />
            Add parameter
          </Button>
          <Button
            data-testid="release-amend-confirm-button"
            type="button"
            disabled={props.isPublishing || !isBufferReady()}
            onClick={() => props.onPublish(bufferEnvironmentName(), rows.map(entry => ({ ...entry })))}
          >
            <MIcon name="check" class="text-[16px]" />
            {props.isPublishing ? "Creating…" : "Create release"}
          </Button>
          <Button data-testid="release-amend-cancel-button" type="button" variant="outline" disabled={props.isPublishing} onClick={props.onCancel}>
            <MIcon name="close" class="text-[16px]" />
            Cancel
          </Button>
        </div>
      </div>

      <Show when={isBufferReady()} fallback={<div class="skeleton h-40 w-full rounded-xl" />}>
        <Show
          when={rows.length > 0}
          fallback={<div class="bg-surface-container rounded-xl px-4 py-8 text-center text-[13px] text-on-surface-variant">This release has no parameters.</div>}
        >
          <div class="flex flex-col gap-2 sm:flex-row sm:items-center">
            <Input
              type="text"
              value={search()}
              onInput={event => setSearch(event.currentTarget.value)}
              placeholder="Search parameters…"
              aria-label="Search parameters"
              class="h-10 sm:w-72"
              leftIcon="search"
            />
          </div>
          <ProjectParamsTable
            isLoading={false}
            projectId={props.projectId}
            activeEnvName={bufferEnvironmentName()}
            filteredConfig={filteredRows()}
            onSelectEntry={selectEntry}
            onDeleteEntry={setDeleteKey}
            onUpdateValue={(entry, nextValue) => {
              const index = rows.findIndex(row => row.key === entry.key);
              if (index >= 0) setRows(index, "value", nextValue);
            }}
            canManage
            search={search()}
          />
        </Show>
      </Show>

      <ProjectParamPanel
        open={creating() || !!selectedEntry()}
        mode={creating() ? "create" : "amend"}
        entry={selectedEntry()}
        projectId={props.projectId}
        environmentName={bufferEnvironmentName() || props.environmentName}
        releaseVersion={props.targetVersion}
        existingEntries={normalizedRows()}
        canManage
        isSaving={false}
        historyVersions={historyQuery.data ?? []}
        isHistoryLoading={historyQuery.isLoading}
        isHistoryActionPending={false}
        onRequestClose={() => requestAction(closePanel, [RELEASE_AMEND_PANEL_DRAFT])}
        onDirtyChange={setPanelDirty}
        onSave={savePanel}
        onHistoryOpen={setHistoryKey}
        onHistoryAction={useHistoryVersion}
      />

      <ConfirmDialog
        open={deleteKey() !== null}
        title="Remove Parameter?"
        message={<>Remove <span class="text-primary font-mono font-bold">{deleteKey()}</span> from this release draft?</>}
        confirmLabel="Remove Parameter"
        onConfirm={() => {
          const key = deleteKey();
          if (key) setRows(rows.filter(row => row.key !== key));
          setDeleteKey(null);
        }}
        onCancel={() => setDeleteKey(null)}
        testId="delete-amend-parameter-dialog"
      />
    </section>
  );
}
