import { writeClipboard } from "@solid-primitives/clipboard";
import { createTimer } from "@solid-primitives/timer";
import { useBeforeLeave, useNavigate, useParams, useSearchParams } from "@solidjs/router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/solid-query";
import { Show, createEffect, createMemo, createSignal, onCleanup } from "solid-js";

import { configEntryService } from "../../../entities/project/api/config-entry.service";
import { configReleaseService } from "../../../entities/project/api/config-release.service";
import { localParamMetadataService } from "../../../entities/project/api/metadata.service";
import { projectKeys } from "../../../entities/project/queries/keys";
import type { ParsedImport } from "../../../features/project-bulk-import/ProjectBulkImport";
import { ProjectBulkImport } from "../../../features/project-bulk-import/ProjectBulkImport";
import { ParameterShareDialog } from "../../../features/project-param-share/ParameterShareDialog";
import { ProjectParamsTab } from "../../../features/project-params/ProjectParamsTab";
import { ReleaseAmendPanel } from "../../../features/project-releases/ReleaseAmendPanel";
import { useEscapeKey } from "../../../shared/hooks/useEscapeKey";
import {
  useUnsavedChanges,
  useUnsavedChangesBlocker
} from "../../../shared/hooks/useUnsavedChanges";
import { MSG } from "../../../shared/lib/messages";
import { ConfirmDialog } from "../../../shared/ui/confirm-dialog";
import { useToast } from "../../../shared/ui/toast";
import type {
  ConfigEntry,
  ConfigEntryVersion,
  CreateConfigEntryRequest,
  CreateParameterShareLinkRequest
} from "../../../types";
import { ProjectSectionLayout } from "../components/ProjectSectionLayout";
import { useProjectContext } from "../hooks/useProjectContext";
import { useReleaseActions } from "../hooks/useReleaseActions";

const CREATE_PARAMETER_DRAFT = "parameter-create-draft";
const EDIT_PARAMETER_DRAFT = "parameter-edit-draft";

const errorMessage = (caught: unknown, fallback: string) =>
  caught instanceof Error && caught.message ? caught.message : fallback;

export default function ParametersSection() {
  const params = useParams<{ slug: string }>();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const queryClient = useQueryClient();
  const { addToast } = useToast();

  const releaseDraftVersion = () =>
    typeof searchParams.release === "string" ? searchParams.release : undefined;
  const viewedReleaseVersion = () =>
    typeof searchParams.viewRelease === "string" ? searchParams.viewRelease : undefined;
  const amendSourceVersion = () =>
    typeof searchParams.amend === "string" ? searchParams.amend : undefined;

  const [paramSearch, setParamSearch] = createSignal("");
  const [copiedKey, setCopiedKey] = createSignal<string | null>(null);
  const [showConfigForm, setShowConfigForm] = createSignal(false);
  const [autoOpenedConfigFormsByEnvironment, setAutoOpenedConfigFormsByEnvironment] = createSignal<
    Record<string, boolean>
  >({});
  const [confirmDeleteEntry, setConfirmDeleteEntry] = createSignal<string | null>(null);
  const [editingEntry, setEditingEntry] = createSignal<ConfigEntry | null>(null);
  const [editHistoryQueryKey, setEditHistoryQueryKey] = createSignal("");
  const [sharingEntry, setSharingEntry] = createSignal<ConfigEntry | null>(null);
  const [shareLinksQueryKey, setShareLinksQueryKey] = createSignal("");
  const [generatedShareUrl, setGeneratedShareUrl] = createSignal<string | null>(null);
  const [revokingShareLinkId, setRevokingShareLinkId] = createSignal<number | null>(null);
  const [editDescription, setEditDescription] = createSignal("");
  const [showBulkImport, setShowBulkImport] = createSignal(false);
  const [pendingReleaseExit, setPendingReleaseExit] = createSignal<(() => void) | null>(null);
  const [createDraftDirty, setCreateDraftDirty] = createSignal(false);
  const [editDraftDirty, setEditDraftDirty] = createSignal(false);
  const { requestAction, isPromptOpen } = useUnsavedChanges();

  const closeCreateDraft = () => {
    setCreateDraftDirty(false);
    setShowConfigForm(false);
  };

  const closeEditDraft = () => {
    setEditDraftDirty(false);
    setEditingEntry(null);
    setEditHistoryQueryKey("");
  };

  useUnsavedChangesBlocker({
    id: CREATE_PARAMETER_DRAFT,
    isDirty: createDraftDirty,
    discard: closeCreateDraft
  });
  useUnsavedChangesBlocker({
    id: EDIT_PARAMETER_DRAFT,
    isDirty: editDraftDirty,
    discard: closeEditDraft
  });

  const isViewingReleaseSnapshot = () => !!viewedReleaseVersion();
  const isAmendMode = () => !!amendSourceVersion();

  const {
    projectsQuery,
    project,
    projectId,
    activeEnvName,
    activeEnvironmentKey,
    canManageProject
  } = useProjectContext();

  const configQuery = useQuery(() => ({
    queryKey: projectKeys.configEntries(params.slug, activeEnvName()),
    queryFn: () => configEntryService.getAll(projectId(), activeEnvName()),
    enabled:
      !!project() && !!activeEnvName() && !isViewingReleaseSnapshot() && !isAmendMode()
  }));

  const releaseDetailsQuery = useQuery(() => ({
    queryKey: projectKeys.configReleaseDetails(
      params.slug,
      activeEnvName(),
      viewedReleaseVersion() ?? ""
    ),
    queryFn: () =>
      configReleaseService.get(projectId(), activeEnvName(), viewedReleaseVersion() ?? ""),
    enabled: !!project() && !!activeEnvName() && isViewingReleaseSnapshot(),
    staleTime: 60_000
  }));

  const amendSourceQuery = useQuery(() => ({
    queryKey: projectKeys.configReleaseDetails(
      params.slug,
      activeEnvName(),
      amendSourceVersion() ?? ""
    ),
    queryFn: () =>
      configReleaseService.get(projectId(), activeEnvName(), amendSourceVersion() ?? ""),
    enabled: !!project() && !!activeEnvName() && isAmendMode(),
    staleTime: 60_000
  }));

  const configHistoryQuery = useQuery(() => ({
    queryKey: projectKeys.configEntryHistory(params.slug, activeEnvName(), editHistoryQueryKey()),
    queryFn: () => configEntryService.history(projectId(), activeEnvName(), editHistoryQueryKey()),
    enabled:
      !!project() &&
      !!activeEnvName() &&
      !!editHistoryQueryKey() &&
      !isViewingReleaseSnapshot(),
    staleTime: 60_000
  }));

  const parameterShareLinksQuery = useQuery(() => ({
    queryKey: projectKeys.configEntryShareLinks(params.slug, activeEnvName(), shareLinksQueryKey()),
    queryFn: () =>
      configEntryService.listShareLinks(projectId(), activeEnvName(), shareLinksQueryKey()),
    enabled:
      !!project() &&
      !!activeEnvName() &&
      !!shareLinksQueryKey() &&
      !isViewingReleaseSnapshot(),
    staleTime: 60_000
  }));

  const releases = createMemo(() => []);

  const { publishReleaseMutation } = useReleaseActions({
    projectId,
    activeEnvName,
    releases
  });

  const shouldConfirmReleaseExit = () =>
    !!releaseDraftVersion() && !publishReleaseMutation.isPending;

  useBeforeLeave(event => {
    if (!shouldConfirmReleaseExit() || event.defaultPrevented) {
      return;
    }

    event.preventDefault();
    setPendingReleaseExit(() => () => event.retry(true));
  });

  createEffect(() => {
    if (!shouldConfirmReleaseExit()) {
      return;
    }

    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = "";
    };

    window.addEventListener("beforeunload", handleBeforeUnload);
    onCleanup(() => window.removeEventListener("beforeunload", handleBeforeUnload));
  });

  createTimer(
    () => setCopiedKey(null),
    () => (copiedKey() ? 1500 : false),
    setTimeout
  );

  const normalizeContentType = (contentType: string): ConfigEntry["contentType"] =>
    contentType === "number" ||
    contentType === "boolean" ||
    contentType === "json" ||
    contentType === "text"
      ? contentType
      : "text";

  const normalizeScope = (scope: string): ConfigEntry["scope"] =>
    scope === "client" || scope === "server" || scope === "all" ? scope : "all";

  const parameterEntries = createMemo<ConfigEntry[]>(() => {
    if (isViewingReleaseSnapshot()) {
      const release =
        releaseDetailsQuery.status === "success" ? releaseDetailsQuery.data : undefined;

      return (release?.entries ?? []).map(entry => ({
        project: release?.project ?? projectId(),
        environment: release?.environment ?? activeEnvName(),
        key: entry.key,
        value: entry.value,
        contentType: normalizeContentType(entry.contentType),
        scope: normalizeScope(entry.scope),
        activeVersion: 1,
        createdAt: release?.createdAt ?? "",
        updatedAt: release?.createdAt ?? ""
      }));
    }

    return configQuery.status === "success" ? (configQuery.data ?? []) : [];
  });

  const parametersLoading = createMemo(() =>
    isViewingReleaseSnapshot() ? releaseDetailsQuery.isLoading : configQuery.isLoading
  );

  const filteredConfig = createMemo(() => {
    const q = paramSearch().toLowerCase().trim();
    const data = parameterEntries();
    if (!q) return data;
    return data.filter(
      (entry: ConfigEntry) =>
        entry.key.toLowerCase().includes(q) ||
        entry.value.toLowerCase().includes(q) ||
        localParamMetadataService
          .getMeta(projectId(), activeEnvName(), entry.key)
          .displayName.toLowerCase()
          .includes(q)
    );
  });

  createEffect(() => {
    const environmentKey = activeEnvironmentKey();
    const currentConfigEntries = configQuery.status === "success" ? (configQuery.data ?? []) : [];
    const hasAutoOpened = environmentKey
      ? autoOpenedConfigFormsByEnvironment()[environmentKey]
      : false;
    const shouldAutoOpen =
      !isViewingReleaseSnapshot() &&
      configQuery.status === "success" &&
      canManageProject() &&
      !!environmentKey &&
      currentConfigEntries.length === 0;

    if (shouldAutoOpen && !hasAutoOpened) {
      setShowConfigForm(true);
      setAutoOpenedConfigFormsByEnvironment(current => ({
        ...current,
        [environmentKey]: true
      }));
      return;
    }

    if (!shouldAutoOpen && environmentKey && hasAutoOpened) {
      setAutoOpenedConfigFormsByEnvironment(current => {
        const next = { ...current };
        delete next[environmentKey];
        return next;
      });
    }
  });

  createEffect(() => {
    const currentProjectSlug = project()?.urlSlug ?? "";
    const currentEnvironmentName = activeEnvName();

    if (!currentProjectSlug && !currentEnvironmentName) {
      return;
    }

    setEditingEntry(null);
    setEditHistoryQueryKey("");
    setSharingEntry(null);
    setShareLinksQueryKey("");
    setGeneratedShareUrl(null);
    setConfirmDeleteEntry(null);
    setEditDescription("");
  });

  const copyValue = async (key: string, value: string) => {
    try {
      await writeClipboard(value);
      setCopiedKey(key);
      addToast(MSG.COPIED, "success");
    } catch {
      addToast(MSG.COPY_FAILED, "error");
    }
  };

  const copyShareUrl = async (value: string) => {
    try {
      await writeClipboard(value);
      addToast(MSG.COPIED, "success");
    } catch {
      addToast(MSG.COPY_FAILED, "error");
    }
  };

  const buildShareUrl = (token: string) =>
    `${window.location.origin}/share/${encodeURIComponent(token)}`;

  useEscapeKey(() => {
    if (isPromptOpen()) return;

    requestAction(
      () => {
        closeEditDraft();
        closeCreateDraft();
        setSharingEntry(null);
        setShareLinksQueryKey("");
        setGeneratedShareUrl(null);
        setShowBulkImport(false);
      },
      [CREATE_PARAMETER_DRAFT, EDIT_PARAMETER_DRAFT]
    );
  });

  const createConfigMutation = useMutation(() => ({
    mutationFn: (req: CreateConfigEntryRequest) =>
      configEntryService.upsert(req.projectId, activeEnvName(), req.key, req),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: projectKeys.configEntries(params.slug, activeEnvName())
      });
      setShowConfigForm(false);
      addToast(MSG.PARAM_CREATED, "success");
    },
    onError: error => addToast(errorMessage(error, MSG.PARAM_CREATE_FAILED), "error")
  }));

  const deleteConfigMutation = useMutation(() => ({
    mutationFn: (id: string) => configEntryService.delete(projectId(), activeEnvName(), id),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: projectKeys.configEntries(params.slug, activeEnvName())
      });
      addToast(MSG.PARAM_DELETED, "success");
    },
    onError: () => addToast(MSG.PARAM_DELETE_FAILED, "error")
  }));

  const handleOpenEditDrawer = (entry: ConfigEntry) => {
    const isCurrentEntry = editingEntry()?.key === entry.key;
    const currentProjectId = projectId();
    const currentEnvironmentName = activeEnvName();

    requestAction(() => {
      if (isCurrentEntry) {
        closeEditDraft();
        return;
      }

      setEditDraftDirty(false);
      setEditingEntry(entry);
      setEditHistoryQueryKey("");
      const meta = localParamMetadataService.getMeta(
        currentProjectId,
        currentEnvironmentName,
        entry.key
      );
      setEditDescription(meta.description);

      if (isViewingReleaseSnapshot()) {
        return;
      }

      requestAnimationFrame(() => {
        if (editingEntry()?.key === entry.key) {
          setEditHistoryQueryKey(entry.key);
        }
      });
    }, [EDIT_PARAMETER_DRAFT]);
  };

  const handleOpenShareDialog = (entry: ConfigEntry) => {
    setSharingEntry(entry);
    setShareLinksQueryKey("");
    setGeneratedShareUrl(null);

    requestAnimationFrame(() => {
      if (sharingEntry()?.key === entry.key) {
        setShareLinksQueryKey(entry.key);
      }
    });
  };

  const updateConfigMutation = useMutation(() => ({
    mutationFn: (req: {
      key: string;
      value: string;
      contentType: ConfigEntry["contentType"];
      scope: ConfigEntry["scope"];
      description?: string;
    }) =>
      configEntryService.upsert(projectId(), activeEnvName(), req.key, {
        value: req.value,
        contentType: req.contentType,
        scope: req.scope
      }),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({
        queryKey: projectKeys.configEntries(params.slug, activeEnvName())
      });

      const desc =
        variables.description !== undefined
          ? variables.description.trim()
          : editDescription().trim();

      if (editingEntry()) {
        localParamMetadataService.setMeta(projectId(), activeEnvName(), editingEntry()!.key, {
          description: desc
        });
      }

      queryClient.invalidateQueries({
        queryKey: projectKeys.configEntryHistory(params.slug, activeEnvName(), variables.key)
      });

      setEditingEntry(null);
      addToast(MSG.PARAM_UPDATED, "success");
    },
    onError: error => addToast(errorMessage(error, MSG.PARAM_UPDATE_FAILED), "error")
  }));

  const handleBulkImport = async (selectedItems: ParsedImport[]) => {
    for (const item of selectedItems) {
      const desc = `Imported parameter: ${item.key}`;

      localParamMetadataService.setMeta(projectId(), activeEnvName(), item.key, {
        description: desc
      });
      await configEntryService.upsert(projectId(), activeEnvName(), item.key, {
        value: item.value,
        contentType: item.contentType,
        scope: item.scope
      });
    }

    queryClient.invalidateQueries({
      queryKey: projectKeys.configEntries(params.slug, activeEnvName())
    });
    setShowBulkImport(false);
    addToast(MSG.bulkImportSuccess(selectedItems.length), "success");
  };

  const rollbackConfigMutation = useMutation(() => ({
    mutationFn: (req: { key: string; version: number }) =>
      configEntryService.rollback(projectId(), activeEnvName(), req.key, {
        version: req.version
      }),
    onSuccess: (entry, variables) => {
      queryClient.invalidateQueries({
        queryKey: projectKeys.configEntries(params.slug, activeEnvName())
      });
      queryClient.invalidateQueries({
        queryKey: projectKeys.configEntryHistory(params.slug, activeEnvName(), variables.key)
      });
      setEditingEntry(entry);
      addToast(MSG.PARAM_ROLLED_BACK, "success");
    },
    onError: error => addToast(errorMessage(error, MSG.PARAM_ROLLBACK_FAILED), "error")
  }));

  const handleRollbackVersion = (version: ConfigEntryVersion) => {
    const entry = editingEntry();
    if (entry) {
      rollbackConfigMutation.mutate({
        key: entry.key,
        version: version.version
      });
    }
  };

  const createShareLinkMutation = useMutation(() => ({
    mutationFn: (data: CreateParameterShareLinkRequest) => {
      const entry = sharingEntry();
      if (!entry) {
        throw new Error("No parameter selected");
      }

      return configEntryService.createShareLink(projectId(), activeEnvName(), entry.key, data);
    },
    onSuccess: shareLink => {
      queryClient.invalidateQueries({
        queryKey: projectKeys.configEntryShareLinks(params.slug, activeEnvName(), shareLink.key)
      });
      queryClient.invalidateQueries({
        queryKey: projectKeys.environmentShareLinks(params.slug, activeEnvName())
      });
      setGeneratedShareUrl(buildShareUrl(shareLink.token));
      addToast(MSG.SHARE_LINK_CREATED, "success");
    },
    onError: error => addToast(errorMessage(error, MSG.SHARE_LINK_CREATE_FAILED), "error")
  }));

  const revokeShareLinkMutation = useMutation(() => ({
    mutationFn: (shareLinkId: number) => {
      const entry = sharingEntry();
      if (!entry) {
        throw new Error("No parameter selected");
      }

      return configEntryService.revokeShareLink(
        projectId(),
        activeEnvName(),
        entry.key,
        shareLinkId
      );
    },
    onSuccess: () => {
      const entry = sharingEntry();
      if (entry) {
        queryClient.invalidateQueries({
          queryKey: projectKeys.configEntryShareLinks(params.slug, activeEnvName(), entry.key)
        });
        queryClient.invalidateQueries({
          queryKey: projectKeys.environmentShareLinks(params.slug, activeEnvName())
        });
      }
      addToast(MSG.SHARE_LINK_REVOKED, "success");
    },
    onError: error => addToast(errorMessage(error, MSG.SHARE_LINK_REVOKE_FAILED), "error"),
    onSettled: () => setRevokingShareLinkId(null)
  }));

  return (
    <>
      <ProjectSectionLayout
        section="parameters"
        project={project()}
        projectLoading={projectsQuery.isLoading}
      >
        <Show when={isViewingReleaseSnapshot()}>
          <div
            data-testid="release-view-banner"
            class="border-secondary/25 bg-secondary/5 animate-fade-in flex flex-col gap-3 rounded-2xl border p-4 sm:flex-row sm:items-center sm:justify-between"
          >
            <div class="flex items-center gap-2 text-[13px]">
              <span class="material-symbols-outlined text-secondary text-[18px]">visibility</span>
              <span class="text-on-surface-variant">
                Viewing release{" "}
                <span class="text-secondary font-mono font-bold">{viewedReleaseVersion()}</span>{" "}
                for {activeEnvName()}.
              </span>
            </div>
            <div class="flex flex-wrap justify-end gap-2">
              <button
                data-testid="release-view-back-to-releases-button"
                type="button"
                onClick={() => navigate(`/projects/${params.slug}/releases`)}
                class="bg-surface-container-high text-on-surface hover:bg-surface-bright inline-flex h-9 cursor-pointer items-center gap-1.5 rounded-lg border-0 px-4 text-[12px] font-semibold"
              >
                <span class="material-symbols-outlined text-[16px]">arrow_back</span>
                Back to releases
              </button>
              <button
                data-testid="release-view-back-button"
                type="button"
                onClick={() => navigate(`/projects/${params.slug}`)}
                class="border-outline-variant/30 bg-surface-container-low text-on-surface-variant hover:bg-surface-container inline-flex h-9 cursor-pointer items-center gap-1.5 rounded-lg border px-4 text-[12px] font-semibold"
              >
                <span class="material-symbols-outlined text-[16px]">tune</span>
                Live parameters
              </button>
            </div>
          </div>
        </Show>

        <Show when={isAmendMode()}>
          <ReleaseAmendPanel
            projectId={projectId()}
            environmentName={activeEnvName()}
            sourceVersion={amendSourceVersion()!}
            targetVersion={releaseDraftVersion() ?? ""}
            sourceEntries={amendSourceQuery.data?.entries ?? []}
            isLoading={amendSourceQuery.isLoading}
            isPublishing={publishReleaseMutation.isPending}
            onPublish={(environmentName, entries) =>
              publishReleaseMutation.mutate({
                environmentName,
                request: {
                  version: releaseDraftVersion() ?? "",
                  makeActive: false,
                  entries
                }
              })
            }
            onCancel={() => navigate(`/projects/${params.slug}/releases`)}
          />
        </Show>

        <Show when={canManageProject() && releaseDraftVersion() && !isAmendMode()}>
          <div
            data-testid="release-draft-banner"
            class="border-primary/25 bg-primary/5 animate-fade-in flex flex-col gap-3 rounded-2xl border p-4 sm:flex-row sm:items-center sm:justify-between"
          >
            <div class="flex items-center gap-2 text-[13px]">
              <span class="material-symbols-outlined text-primary text-[18px]">
                deployed_code_history
              </span>
              <span class="text-on-surface-variant">
                Composing release{" "}
                <span class="text-primary font-mono font-bold">{releaseDraftVersion()}</span> -
                adjust the parameters below, then create it.
              </span>
            </div>
            <div class="flex shrink-0 flex-wrap justify-end gap-2">
              <button
                data-testid="release-create-confirm-button"
                type="button"
                disabled={publishReleaseMutation.isPending || !activeEnvName()}
                onClick={() =>
                  publishReleaseMutation.mutate({
                    environmentName: activeEnvName(),
                    request: {
                      version: releaseDraftVersion()!,
                      makeActive: false
                    }
                  })
                }
                class="bg-primary text-on-primary inline-flex h-9 cursor-pointer items-center gap-1.5 rounded-lg border-0 px-4 text-[12px] font-semibold transition-all hover:brightness-105 active:scale-[0.98] disabled:opacity-50"
              >
                <span class="material-symbols-outlined text-[16px]">check</span>
                {publishReleaseMutation.isPending ? "Creating..." : "Create release"}
              </button>
              <button
                data-testid="release-create-cancel-button"
                type="button"
                disabled={publishReleaseMutation.isPending}
                onClick={() => navigate(`/projects/${params.slug}/releases`)}
                class="border-outline-variant/30 bg-surface-container-low text-on-surface-variant hover:bg-surface-container inline-flex h-9 cursor-pointer items-center gap-1.5 rounded-lg border px-4 text-[12px] font-semibold transition-all disabled:opacity-50"
              >
                <span class="material-symbols-outlined text-[16px]">close</span>
                Cancel
              </button>
            </div>
          </div>
        </Show>

        <Show when={!isAmendMode()}>
          <ProjectParamsTab
            activeEnvName={activeEnvName()}
            configEntries={parameterEntries()}
            filteredConfig={filteredConfig()}
            isLoading={parametersLoading()}
            paramSearch={paramSearch()}
            onParamSearch={setParamSearch}
            onToggleBulkImport={() => {
              const nextShowBulkImport = !showBulkImport();
              requestAction(() => {
                setShowBulkImport(nextShowBulkImport);
                closeCreateDraft();
              }, [CREATE_PARAMETER_DRAFT]);
            }}
            onToggleConfigForm={() => {
              const nextShowConfigForm = !showConfigForm();
              const toggleForm = () => {
                setShowConfigForm(nextShowConfigForm);
                setShowBulkImport(false);
              };

              if (showConfigForm()) {
                requestAction(toggleForm, [CREATE_PARAMETER_DRAFT]);
              } else {
                toggleForm();
              }
            }}
            showConfigForm={showConfigForm()}
            bulkImportPanel={
              !isViewingReleaseSnapshot() && canManageProject() && showBulkImport() ? (
                <ProjectBulkImport
                  onCancel={() => setShowBulkImport(false)}
                  onImport={handleBulkImport}
                  existingEntries={parameterEntries()}
                  isPending={updateConfigMutation.isPending}
                  addToast={addToast}
                />
              ) : undefined
            }
            canManage={canManageProject() && !isViewingReleaseSnapshot()}
            isReadOnly={isViewingReleaseSnapshot()}
            viewingReleaseVersion={viewedReleaseVersion()}
            createForm={{
              onCancel: () => requestAction(closeCreateDraft, [CREATE_PARAMETER_DRAFT]),
              onSubmit: data => {
                if (!canManageProject()) return;
                localParamMetadataService.setMeta(projectId(), activeEnvName(), data.key, {
                  description: data.description
                });
                createConfigMutation.mutate({
                  projectId: projectId(),
                  key: data.key,
                  value: data.value,
                  contentType: data.contentType,
                  scope: data.scope
                });
              },
              isPending: createConfigMutation.isPending,
              onDirtyChange: setCreateDraftDirty
            }}
            table={{
              isLoading: parametersLoading(),
              projectId: projectId(),
              activeEnvName: activeEnvName(),
              filteredConfig: filteredConfig(),
              editingEntry: editingEntry(),
              onSelectEntry: handleOpenEditDrawer,
              onShareEntry: handleOpenShareDialog,
              onDeleteEntry: setConfirmDeleteEntry,
              canManage: canManageProject() && !isViewingReleaseSnapshot(),
              copiedKey: copiedKey(),
              onCopyValue: copyValue,
              getParamMeta: (proj, env, key) =>
                localParamMetadataService.getMeta(proj, env, key),
              initialDescription: editDescription(),
              onCloseEntry: () => {
                requestAction(closeEditDraft, [EDIT_PARAMETER_DRAFT]);
              },
              onEditDirtyChange: setEditDraftDirty,
              onSaveSettings: data => {
                if (!canManageProject()) return;
                setEditDescription(data.description);
                updateConfigMutation.mutate({
                  key: editingEntry()!.key,
                  value: data.value,
                  contentType: data.contentType,
                  scope: data.scope,
                  description: data.description
                });
              },
              isSaving: updateConfigMutation.isPending,
              historyVersions:
                configHistoryQuery.status === "success" ? (configHistoryQuery.data ?? []) : [],
              isHistoryLoading: configHistoryQuery.isLoading,
              isRollingBack: rollbackConfigMutation.isPending,
              onRollbackVersion: handleRollbackVersion,
              search: paramSearch(),
              isReadOnly: isViewingReleaseSnapshot(),
              releaseVersion: viewedReleaseVersion()
            }}
          />
        </Show>

        <ParameterShareDialog
          entry={sharingEntry()}
          shareLinks={
            parameterShareLinksQuery.status === "success"
              ? (parameterShareLinksQuery.data ?? [])
              : []
          }
          generatedUrl={generatedShareUrl()}
          isLoading={parameterShareLinksQuery.isLoading}
          isCreating={createShareLinkMutation.isPending}
          revokingId={revokingShareLinkId()}
          onClose={() => {
            setSharingEntry(null);
            setShareLinksQueryKey("");
            setGeneratedShareUrl(null);
          }}
          onCreate={data => createShareLinkMutation.mutate(data)}
          onRevoke={shareLinkId => {
            setRevokingShareLinkId(shareLinkId);
            revokeShareLinkMutation.mutate(shareLinkId);
          }}
          onCopy={copyShareUrl}
          buildShareUrl={buildShareUrl}
        />
      </ProjectSectionLayout>

      <ConfirmDialog
        open={confirmDeleteEntry() !== null}
        title="Delete Parameter?"
        message={
          <>
            Permanently delete{" "}
            <span class="text-primary font-mono font-bold">{confirmDeleteEntry()}</span> from the{" "}
            <span class="text-on-surface font-medium">{activeEnvName()}</span> environment?
          </>
        }
        confirmLabel="Delete Parameter"
        variant="danger"
        isLoading={deleteConfigMutation.isPending}
        onConfirm={() => {
          const key = confirmDeleteEntry();
          if (key) {
            deleteConfigMutation.mutate(key);
            setConfirmDeleteEntry(null);
          }
        }}
        onCancel={() => setConfirmDeleteEntry(null)}
        testId="delete-parameter-dialog"
        confirmTestId="delete-parameter-confirm-button"
        cancelTestId="delete-parameter-cancel-button"
      />

      <ConfirmDialog
        open={pendingReleaseExit() !== null}
        title="Exit Release Creation?"
        message={
          <>
            You are changing parameters for release{" "}
            <span class="text-primary font-mono font-bold">{releaseDraftVersion()}</span>. Exit
            this process and discard the in-progress release changes?
          </>
        }
        confirmLabel="Exit"
        cancelLabel="Keep Editing"
        variant="warning"
        onConfirm={() => {
          const retry = pendingReleaseExit();
          setPendingReleaseExit(null);
          retry?.();
        }}
        onCancel={() => setPendingReleaseExit(null)}
        testId="release-exit-dialog"
        confirmTestId="release-exit-confirm-button"
        cancelTestId="release-exit-cancel-button"
      />
    </>
  );
}
