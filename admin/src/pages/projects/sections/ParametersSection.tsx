import { writeClipboard } from "@solid-primitives/clipboard";
import { useBeforeLeave, useLocation, useNavigate, useParams, useSearchParams } from "@solidjs/router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/solid-query";
import { Show, createEffect, createMemo, createSignal, on, onCleanup } from "solid-js";

import { configEntryService } from "../../../entities/project/api/config-entry.service";
import { configReleaseService } from "../../../entities/project/api/config-release.service";
import { localParamMetadataService } from "../../../entities/project/api/metadata.service";
import { projectKeys } from "../../../entities/project/queries/keys";
import type { ParsedImport } from "../../../features/project-bulk-import/ProjectBulkImport";
import { ProjectBulkImport } from "../../../features/project-bulk-import/ProjectBulkImport";
import { ParameterShareDialog } from "../../../features/project-param-share/ParameterShareDialog";
import {
  ProjectParamPanel,
  type ParameterPanelSaveData
} from "../../../features/project-param-edit/ProjectParamPanel";
import { ProjectParamsTab } from "../../../features/project-params/ProjectParamsTab";
import { ReleaseDraftPanel } from "../../../features/project-releases/ReleaseDraftPanel";
import { useEscapeKey } from "../../../shared/hooks/useEscapeKey";
import {
  useUnsavedChanges,
  useUnsavedChangesBlocker
} from "../../../shared/hooks/useUnsavedChanges";
import { MSG } from "../../../shared/lib/messages";
import { getProjectPageSection } from "../../../shared/lib/project-navigation";
import { ConfirmDialog } from "../../../shared/ui/confirm-dialog";
import { useToast } from "../../../shared/ui/toast";
import type {
  ConfigEntry,
  ConfigEntryVersion,
  CreateParameterShareLinkRequest
} from "../../../types";
import { ProjectSectionLayout } from "../components/ProjectSectionLayout";
import { useProjectContext } from "../hooks/useProjectContext";
import { useReleaseActions } from "../hooks/useReleaseActions";

const CREATE_PARAMETER_DRAFT = "parameter-create-draft";
const EDIT_PARAMETER_DRAFT = "parameter-edit-draft";

interface ProjectEnvironmentTarget {
  projectId: string;
  projectSlug: string;
  environmentName: string;
}

interface ConfigEntryWrite {
  key: string;
  value: string;
  contentType: ConfigEntry["contentType"];
  scope: ConfigEntry["scope"];
  description?: string | null;
  unit?: string | null;
}

const errorMessage = (caught: unknown, fallback: string) =>
  caught instanceof Error && caught.message ? caught.message : fallback;

export default function ParametersSection() {
  const params = useParams<{ slug: string }>();
  const location = useLocation();
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
  const [updatingValueKey, setUpdatingValueKey] = createSignal<string | null>(null);
  const [showBulkImport, setShowBulkImport] = createSignal(false);
  const [pendingReleaseExit, setPendingReleaseExit] = createSignal<(() => void) | null>(null);
  const [createDraftDirty, setCreateDraftDirty] = createSignal(false);
  const [editDraftDirty, setEditDraftDirty] = createSignal(false);
  let panelOpener: HTMLElement | undefined;
  const { requestAction, isPromptOpen } = useUnsavedChanges();

  const returnPanelFocus = () => {
    const opener = panelOpener;
    panelOpener = undefined;
    requestAnimationFrame(() => opener?.focus({ preventScroll: true }));
  };

  const closeCreateDraft = () => {
    setCreateDraftDirty(false);
    setShowConfigForm(false);
    returnPanelFocus();
  };

  const closeEditDraft = () => {
    setEditDraftDirty(false);
    setEditingEntry(null);
    setEditHistoryQueryKey("");
    returnPanelFocus();
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
  const isDefaultParametersView = () =>
    !releaseDraftVersion() && !viewedReleaseVersion() && !amendSourceVersion();

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
    enabled: !!project() && !!activeEnvName()
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
      !!editHistoryQueryKey(),
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
      isDefaultParametersView(),
    staleTime: 60_000
  }));

  const releases = createMemo(() => []);

  const { publishReleaseMutation } = useReleaseActions({
    projectId,
    activeEnvName,
    releases
  });

  const shouldConfirmReleaseExit = () =>
    !!releaseDraftVersion() && !isAmendMode() && !publishReleaseMutation.isPending;

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
        updatedAt: release?.createdAt ?? "",
        description: entry.description,
        unit: entry.unit
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
        (entry.description ?? localParamMetadataService
          .getMeta(projectId(), activeEnvName(), entry.key)
          .description)
          .toLowerCase()
          .includes(q) ||
        (entry.unit ?? "").toLowerCase().includes(q)
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

  createEffect(on(
    () => `${project()?.urlSlug ?? ""}:${activeEnvName()}`,
    () => {
      setEditingEntry(null);
      setEditHistoryQueryKey("");
      setSharingEntry(null);
      setShareLinksQueryKey("");
      setGeneratedShareUrl(null);
      setRevokingShareLinkId(null);
      setConfirmDeleteEntry(null);
      setEditDescription("");
      setShowBulkImport(false);
    },
    { defer: true }
  ));

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

  const currentMutationTarget = (): ProjectEnvironmentTarget => ({
    projectId: projectId(),
    projectSlug: params.slug,
    environmentName: activeEnvName()
  });

  const isCurrentMutationTarget = (target: ProjectEnvironmentTarget) =>
    target.projectId === projectId()
    && target.projectSlug === params.slug
    && target.environmentName === activeEnvName();

  const configEntriesQueryKey = (target = currentMutationTarget()) =>
    projectKeys.configEntries(target.projectSlug, target.environmentName);

  const patchConfigEntry = (target: ProjectEnvironmentTarget, entry: ConfigEntry) => {
    queryClient.setQueryData<ConfigEntry[]>(configEntriesQueryKey(target), current => {
      const entries = current ?? [];
      const index = entries.findIndex(candidate => candidate.key === entry.key);
      if (index === -1) return [...entries, entry];
      return entries.map(candidate => candidate.key === entry.key ? entry : candidate);
    });
  };

  const createConfigMutation = useMutation(() => ({
    mutationFn: ({ target, entry }: {
      target: ProjectEnvironmentTarget;
      entry: ConfigEntryWrite;
    }) => {
      const { key, ...request } = entry;
      return configEntryService.upsert(
        target.projectId,
        target.environmentName,
        key,
        request
      );
    },
    onSuccess: (entry, { target }) => {
      patchConfigEntry(target, entry);
      void queryClient.invalidateQueries({ queryKey: configEntriesQueryKey(target) });
      if (isCurrentMutationTarget(target) && showConfigForm()) {
        setShowConfigForm(false);
        setCreateDraftDirty(false);
        setEditingEntry(entry);
        setEditDescription(entry.description ?? "");
      }
      addToast(MSG.PARAM_CREATED, "success");
    },
    onError: error => addToast(errorMessage(error, MSG.PARAM_CREATE_FAILED), "error")
  }));

  const deleteConfigMutation = useMutation(() => ({
    mutationFn: ({ target, key }: { target: ProjectEnvironmentTarget; key: string }) =>
      configEntryService.delete(target.projectId, target.environmentName, key),
    onSuccess: (_, { target, key }) => {
      queryClient.setQueryData<ConfigEntry[]>(
        configEntriesQueryKey(target),
        current => (current ?? []).filter(entry => entry.key !== key)
      );
      void queryClient.invalidateQueries({ queryKey: configEntriesQueryKey(target) });
      addToast(MSG.PARAM_DELETED, "success");
    },
    onError: () => addToast(MSG.PARAM_DELETE_FAILED, "error")
  }));

  const handleOpenEditDrawer = (entry: ConfigEntry, opener?: HTMLElement) => {
    const isCurrentEntry = editingEntry()?.key === entry.key;
    const currentProjectId = projectId();
    const currentEnvironmentName = activeEnvName();

    if (isCurrentEntry && !showConfigForm()) return;

    requestAction(() => {
      panelOpener = opener;
      setShowConfigForm(false);
      setCreateDraftDirty(false);

      setEditDraftDirty(false);
      setEditingEntry(entry);
      setEditHistoryQueryKey("");
      const localDescription = localParamMetadataService.getMeta(
        currentProjectId,
        currentEnvironmentName,
        entry.key
      ).description;
      setEditDescription(entry.description ?? localDescription);

    }, [CREATE_PARAMETER_DRAFT, EDIT_PARAMETER_DRAFT]);
  };

  const handleOpenCreatePanel = (opener: HTMLElement) => {
    requestAction(() => {
      panelOpener = opener;
      setEditingEntry(null);
      setEditHistoryQueryKey("");
      setEditDraftDirty(false);
      setShowBulkImport(false);
      setShowConfigForm(true);
    }, [CREATE_PARAMETER_DRAFT, EDIT_PARAMETER_DRAFT]);
  };

  const closeShareDialog = () => {
    setSharingEntry(null);
    setShareLinksQueryKey("");
    setGeneratedShareUrl(null);
  };

  createEffect(on(isDefaultParametersView, isDefaultView => {
    if (!isDefaultView) closeShareDialog();
  }));

  const handleOpenShareDialog = (entry: ConfigEntry) => {
    if (!isDefaultParametersView()) return;

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
    mutationFn: ({ target, entry }: {
      target: ProjectEnvironmentTarget;
      entry: ConfigEntryWrite;
    }) => configEntryService.upsert(target.projectId, target.environmentName, entry.key, {
        value: entry.value,
        contentType: entry.contentType,
        scope: entry.scope,
        ...(entry.description !== undefined ? { description: entry.description } : {}),
        ...(entry.unit !== undefined ? { unit: entry.unit } : {})
      }),
    onSuccess: (entry, { target }) => {
      patchConfigEntry(target, entry);
      void queryClient.invalidateQueries({ queryKey: configEntriesQueryKey(target) });
      void queryClient.invalidateQueries({
        queryKey: projectKeys.configEntryHistory(
          target.projectSlug,
          target.environmentName,
          entry.key
        )
      });
      if (isCurrentMutationTarget(target) && editingEntry()?.key === entry.key) {
        setEditingEntry(entry);
        setEditDescription(entry.description ?? "");
        setEditDraftDirty(false);
      }
      addToast(MSG.PARAM_UPDATED, "success");
    },
    onError: error => addToast(errorMessage(error, MSG.PARAM_UPDATE_FAILED), "error")
  }));

  const handleBulkImport = async (selectedItems: ParsedImport[]) => {
    const target = currentMutationTarget();
    for (const item of selectedItems) {
      const desc = `Imported parameter: ${item.key}`;

      await configEntryService.upsert(target.projectId, target.environmentName, item.key, {
        value: item.value,
        contentType: item.contentType,
        scope: item.scope,
        description: desc
      });
    }

    void queryClient.invalidateQueries({ queryKey: configEntriesQueryKey(target) });
    if (isCurrentMutationTarget(target)) setShowBulkImport(false);
    addToast(MSG.bulkImportSuccess(selectedItems.length), "success");
  };

  const rollbackConfigMutation = useMutation(() => ({
    mutationFn: ({ target, key, version }: {
      target: ProjectEnvironmentTarget;
      key: string;
      version: number;
    }) =>
      configEntryService.rollback(target.projectId, target.environmentName, key, {
        version
      }),
    onSuccess: (entry, { target }) => {
      patchConfigEntry(target, entry);
      void queryClient.invalidateQueries({ queryKey: configEntriesQueryKey(target) });
      void queryClient.invalidateQueries({
        queryKey: projectKeys.configEntryHistory(
          target.projectSlug,
          target.environmentName,
          entry.key
        )
      });
      if (isCurrentMutationTarget(target) && editingEntry()?.key === entry.key) {
        setEditingEntry(entry);
        setEditDescription(entry.description ?? "");
        setEditDraftDirty(false);
      }
      addToast(MSG.PARAM_ROLLED_BACK, "success");
    },
    onError: error => addToast(errorMessage(error, MSG.PARAM_ROLLBACK_FAILED), "error")
  }));

  const restoreVersion = async (version: ConfigEntryVersion) => {
    const entry = editingEntry();
    if (!entry) return;
    return rollbackConfigMutation.mutateAsync({
      target: currentMutationTarget(),
      key: entry.key,
      version: version.version
    });
  };

  const createShareLinkMutation = useMutation(() => ({
    mutationFn: ({ target, key, data }: {
      target: ProjectEnvironmentTarget;
      key: string;
      data: CreateParameterShareLinkRequest;
    }) => configEntryService.createShareLink(
      target.projectId,
      target.environmentName,
      key,
      data
    ),
    onSuccess: (shareLink, { target, key }) => {
      queryClient.invalidateQueries({
        queryKey: projectKeys.configEntryShareLinks(
          target.projectSlug,
          target.environmentName,
          key
        )
      });
      queryClient.invalidateQueries({
        queryKey: projectKeys.environmentShareLinks(target.projectSlug, target.environmentName)
      });
      if (isCurrentMutationTarget(target) && sharingEntry()?.key === key) {
        setGeneratedShareUrl(buildShareUrl(shareLink.token));
      }
      addToast(MSG.SHARE_LINK_CREATED, "success");
    },
    onError: error => addToast(errorMessage(error, MSG.SHARE_LINK_CREATE_FAILED), "error")
  }));

  const revokeShareLinkMutation = useMutation(() => ({
    mutationFn: ({ target, key, shareLinkId }: {
      target: ProjectEnvironmentTarget;
      key: string;
      shareLinkId: number;
    }) => configEntryService.revokeShareLink(
      target.projectId,
      target.environmentName,
      key,
      shareLinkId
    ),
    onSuccess: (_, { target, key }) => {
      queryClient.invalidateQueries({
        queryKey: projectKeys.configEntryShareLinks(
          target.projectSlug,
          target.environmentName,
          key
        )
      });
      queryClient.invalidateQueries({
        queryKey: projectKeys.environmentShareLinks(target.projectSlug, target.environmentName)
      });
      addToast(MSG.SHARE_LINK_REVOKED, "success");
    },
    onError: error => addToast(errorMessage(error, MSG.SHARE_LINK_REVOKE_FAILED), "error"),
    onSettled: (_, __, { target, key, shareLinkId }) => {
      if (
        isCurrentMutationTarget(target)
        && sharingEntry()?.key === key
        && revokingShareLinkId() === shareLinkId
      ) {
        setRevokingShareLinkId(null);
      }
    }
  }));

  const handlePanelSave = async (data: ParameterPanelSaveData) => {
    if (!canManageProject()) throw new Error("You do not have permission to manage parameters.");

    if (showConfigForm()) {
      return createConfigMutation.mutateAsync({
        target: currentMutationTarget(),
        entry: {
          key: data.key,
          value: data.value,
          contentType: data.contentType,
          scope: data.scope,
          description: data.description,
          unit: data.unit
        }
      });
    }

    const selected = editingEntry();
    if (!selected) throw new Error("No parameter selected.");
    return updateConfigMutation.mutateAsync({
      target: currentMutationTarget(),
      entry: {
        key: selected.key,
        value: data.value,
        contentType: data.contentType,
        scope: data.scope,
        description: data.description,
        unit: selected.contentType === "number"
          && data.contentType === "number"
          && data.unit === null
          ? ""
          : data.unit
      }
    });
  };

  return (
    <>
      <ProjectSectionLayout
        section={getProjectPageSection(location.pathname, location.search) ?? "parameters"}
        project={project()}
        projectLoading={projectsQuery.isLoading}
      >
        <Show when={isViewingReleaseSnapshot()}>
          <div
            data-testid="release-view-banner"
            class="border-secondary/25 bg-secondary/5 animate-fade-in flex flex-col gap-3 rounded-2xl border p-4 sm:flex-row sm:items-center sm:justify-between"
          >
            <div class="flex items-center gap-2 text-[14px]">
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
                class="bg-surface-container-high text-on-surface hover:bg-surface-bright inline-flex h-9 cursor-pointer items-center gap-1.5 rounded-lg border-0 px-4 text-[13px] font-semibold"
              >
                <span class="material-symbols-outlined text-[16px]">arrow_back</span>
                Back to releases
              </button>
              <button
                data-testid="release-view-back-button"
                type="button"
                onClick={() => navigate(`/projects/${params.slug}`)}
                class="border-outline-variant/30 bg-surface-container-low text-on-surface-variant hover:bg-surface-container inline-flex h-9 cursor-pointer items-center gap-1.5 rounded-lg border px-4 text-[13px] font-semibold"
              >
                <span class="material-symbols-outlined text-[16px]">tune</span>
                Live parameters
              </button>
            </div>
          </div>
        </Show>

        <Show when={isAmendMode()}>
          <ReleaseDraftPanel
            mode="amend"
            projectId={projectId()}
            environmentName={activeEnvName()}
            sourceVersion={amendSourceVersion()!}
            targetVersion={releaseDraftVersion() ?? ""}
            sourceEntries={amendSourceQuery.data?.entries ?? []}
            sourceReady={amendSourceQuery.status === "success"}
            sourceError={
              amendSourceQuery.isError
                ? errorMessage(amendSourceQuery.error, "The release could not be loaded.")
                : undefined
            }
            onRetrySource={() => void amendSourceQuery.refetch()}
            isPublishing={publishReleaseMutation.isPending}
            onPublish={(environmentName, entries, onPublished) =>
              publishReleaseMutation.mutate({
                environmentName,
                beforeNavigate: onPublished,
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
            <div class="flex items-center gap-2 text-[14px]">
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
                class="bg-primary text-on-primary inline-flex h-9 cursor-pointer items-center gap-1.5 rounded-lg border-0 px-4 text-[13px] font-semibold transition-all hover:brightness-105 active:scale-[0.98] disabled:opacity-50"
              >
                <span class="material-symbols-outlined text-[16px]">check</span>
                {publishReleaseMutation.isPending ? "Creating..." : "Create release"}
              </button>
              <button
                data-testid="release-create-cancel-button"
                type="button"
                disabled={publishReleaseMutation.isPending}
                onClick={() => navigate(`/projects/${params.slug}/releases`)}
                class="border-outline-variant/30 bg-surface-container-low text-on-surface-variant hover:bg-surface-container inline-flex h-9 cursor-pointer items-center gap-1.5 rounded-lg border px-4 text-[13px] font-semibold transition-all disabled:opacity-50"
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
            onAddParameter={handleOpenCreatePanel}
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
            table={{
              isLoading: parametersLoading(),
              projectId: projectId(),
              activeEnvName: activeEnvName(),
              filteredConfig: filteredConfig(),
              onSelectEntry: handleOpenEditDrawer,
              onDeleteEntry: setConfirmDeleteEntry,
              onUpdateValue: async (entry, value) => {
                setUpdatingValueKey(entry.key);
                try {
                  await updateConfigMutation.mutateAsync({
                    target: currentMutationTarget(),
                    entry: {
                      key: entry.key,
                      value,
                      contentType: entry.contentType,
                      scope: entry.scope
                    }
                  });
                } finally {
                  setUpdatingValueKey(null);
                }
              },
              updatingKey: updatingValueKey(),
              canManage: canManageProject() && !isViewingReleaseSnapshot(),
              search: paramSearch(),
              isReadOnly: isViewingReleaseSnapshot()
            }}
          />
        </Show>

        <Show when={!isAmendMode()}>
          <ProjectParamPanel
            open={showConfigForm() || !!editingEntry()}
            mode={
              showConfigForm()
                ? "create"
                : isViewingReleaseSnapshot()
                  ? "snapshot"
                  : "live"
            }
            entry={editingEntry()}
            projectId={projectId()}
            environmentName={activeEnvName()}
            releaseVersion={viewedReleaseVersion()}
            existingEntries={parameterEntries()}
            canManage={canManageProject() && !isViewingReleaseSnapshot()}
            initialDescription={editDescription()}
            isSaving={createConfigMutation.isPending || updateConfigMutation.isPending}
            historyVersions={
              configHistoryQuery.status === "success" ? (configHistoryQuery.data ?? []) : []
            }
            isHistoryLoading={configHistoryQuery.isLoading}
            isHistoryActionPending={rollbackConfigMutation.isPending}
            shareEnabled={
              !!editingEntry()
              && canManageProject()
              && !showConfigForm()
              && isDefaultParametersView()
            }
            shareDisabledReason={
              !canManageProject()
                ? "You do not have permission to create share links."
                : "Share links are available only for default parameters."
            }
            onRequestClose={() => {
              const blocker = showConfigForm() ? CREATE_PARAMETER_DRAFT : EDIT_PARAMETER_DRAFT;
              requestAction(
                showConfigForm() ? closeCreateDraft : closeEditDraft,
                [blocker]
              );
            }}
            onDirtyChange={dirty => {
              if (showConfigForm()) setCreateDraftDirty(dirty);
              else setEditDraftDirty(dirty);
            }}
            onSave={handlePanelSave}
            onHistoryOpen={key => setEditHistoryQueryKey(key)}
            onHistoryAction={restoreVersion}
            onShare={isDefaultParametersView() ? handleOpenShareDialog : undefined}
          />
        </Show>

        <ParameterShareDialog
          entry={isDefaultParametersView() ? sharingEntry() : null}
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
            closeShareDialog();
          }}
          onCreate={data => {
            const entry = sharingEntry();
            if (!entry || !isDefaultParametersView()) return;
            createShareLinkMutation.mutate({
              target: currentMutationTarget(),
              key: entry.key,
              data
            });
          }}
          onRevoke={shareLinkId => {
            const entry = sharingEntry();
            if (!entry || !isDefaultParametersView()) return;
            setRevokingShareLinkId(shareLinkId);
            revokeShareLinkMutation.mutate({
              target: currentMutationTarget(),
              key: entry.key,
              shareLinkId
            });
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
            deleteConfigMutation.mutate({ target: currentMutationTarget(), key });
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
