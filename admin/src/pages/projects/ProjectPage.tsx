import { writeClipboard } from "@solid-primitives/clipboard";
import { createTimer } from "@solid-primitives/timer";
import { Title } from "@solidjs/meta";
import { useNavigate, useParams, useSearchParams } from "@solidjs/router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/solid-query";
import { Show, createEffect, createMemo, createSignal } from "solid-js";

import { configEntryService } from "../../entities/project/api/config-entry.service";
import { configReleaseService } from "../../entities/project/api/config-release.service";
import { environmentService } from "../../entities/project/api/environment.service";
import { projectService } from "../../entities/project/api/project.service";
import { ConfirmDialog } from "../../shared/ui/confirm-dialog";
import { useToast } from "../../shared/ui/toast";

import type { ParsedImport } from "../../features/project-bulk-import/ProjectBulkImport";
import { ProjectBulkImport } from "../../features/project-bulk-import/ProjectBulkImport";
import { ProjectEnvironments } from "../../features/project-environments/ProjectEnvironments";
import { ParameterShareDialog } from "../../features/project-param-share/ParameterShareDialog";
import { ProjectParamsTab } from "../../features/project-params/ProjectParamsTab";
import { ProjectReleases } from "../../features/project-releases/ProjectReleases";
import { ReleaseVersionDialog } from "../../features/project-releases/ReleaseVersionDialog";
import { ProjectShareLinks } from "../../features/project-share-links/ProjectShareLinks";
import { ProjectApiKeys } from "./components/ProjectApiKeys";
import { ProjectPageSkeleton } from "./components/ProjectPageSkeleton";
import { useProjectContext } from "./hooks/useProjectContext";

import {
  localParamMetadataService
} from "../../entities/project/api/metadata.service";
import { projectKeys } from "../../entities/project/queries/keys";
import { useEscapeKey } from "../../shared/hooks/useEscapeKey";
import { MSG } from "../../shared/lib/messages";
import type {
  ConfigEntry,
  ConfigEntryVersion,
  ConfigReleaseEntry,
  CreateParameterShareLinkRequest,
  CreateConfigEntryRequest,
  CreateApiKeyRequest,
  CreateEnvironmentRequest,
  ParameterShareLink,
  PublishConfigReleaseRequest
} from "../../types";

const errorMessage = (caught: unknown, fallback: string) =>
  caught instanceof Error && caught.message ? caught.message : fallback;

type ProjectPageSection = "environments" | "parameters" | "sharedLinks" | "apiKeys" | "releases";

export default function ProjectPage() {
  return <ProjectPageContent section="parameters" />;
}

export function ProjectEnvironmentsPage() {
  return <ProjectPageContent section="environments" />;
}

export function ProjectApiKeysPage() {
  return <ProjectPageContent section="apiKeys" />;
}

export function ProjectShareLinksPage() {
  return <ProjectPageContent section="sharedLinks" />;
}

export function ProjectReleasesPage() {
  return <ProjectPageContent section="releases" />;
}

function ProjectPageContent(props: { section: ProjectPageSection }) {
  const params = useParams<{ slug: string }>();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const queryClient = useQueryClient();
  const { addToast } = useToast();

  const releaseDraftVersion = () =>
    typeof searchParams.release === "string" ? searchParams.release : undefined;
  const viewedReleaseVersion = () =>
    typeof searchParams.viewRelease === "string" ? searchParams.viewRelease : undefined;
  // Source release being amended into a new patch (payload editing; the working
  // configuration is never touched).
  const amendSourceVersion = () =>
    typeof searchParams.amend === "string" ? searchParams.amend : undefined;

  const [paramSearch, setParamSearch] = createSignal("");
  const [copiedKey, setCopiedKey] = createSignal<string | null>(null);
  const [showEnvForm, setShowEnvForm] = createSignal(false);
  const [showConfigForm, setShowConfigForm] = createSignal(false);
  const [showApiKeyForm, setShowApiKeyForm] = createSignal(false);
  const [hasAutoOpenedEnvForm, setHasAutoOpenedEnvForm] = createSignal(false);
  const [hasAutoOpenedApiKeyForm, setHasAutoOpenedApiKeyForm] = createSignal(false);
  const [autoOpenedConfigFormsByEnvironment, setAutoOpenedConfigFormsByEnvironment] = createSignal<
    Record<string, boolean>
  >({});
  const [confirmDeleteEntry, setConfirmDeleteEntry] = createSignal<string | null>(null);
  const [confirmDeleteEnv, setConfirmDeleteEnv] = createSignal<string | null>(null);
  const [editingEntry, setEditingEntry] = createSignal<ConfigEntry | null>(null);
  const [editHistoryQueryKey, setEditHistoryQueryKey] = createSignal("");
  const [sharingEntry, setSharingEntry] = createSignal<ConfigEntry | null>(null);
  const [shareLinksQueryKey, setShareLinksQueryKey] = createSignal("");
  const [generatedShareUrl, setGeneratedShareUrl] = createSignal<string | null>(null);
  const [revokingShareLinkId, setRevokingShareLinkId] = createSignal<number | null>(null);
  const [editDescription, setEditDescription] = createSignal("");
  const [showBulkImport, setShowBulkImport] = createSignal(false);
  const [deletingApiKeyId, setDeletingApiKeyId] = createSignal<string | null>(null);
  const [confirmDeleteRelease, setConfirmDeleteRelease] = createSignal<string | null>(null);
  const [confirmActivateRelease, setConfirmActivateRelease] = createSignal<string | null>(null);
  const [confirmClearActiveRelease, setConfirmClearActiveRelease] = createSignal(false);
  const [deletingReleaseVersion, setDeletingReleaseVersion] = createSignal<string | null>(null);
  const [createVersionOpen, setCreateVersionOpen] = createSignal(false);
  const [amendEntries, setAmendEntries] = createSignal<ConfigEntry[]>([]);
  const [amendDescriptions, setAmendDescriptions] = createSignal<Record<string, string>>({});
  const [amendSeededIdentity, setAmendSeededIdentity] = createSignal<string | null>(null);

  createTimer(
    () => setCopiedKey(null),
    () => (copiedKey() ? 1500 : false),
    setTimeout
  );

  const isParametersPage = () => props.section === "parameters";
  const isEnvironmentsPage = () => props.section === "environments";
  const isSharedLinksPage = () => props.section === "sharedLinks";
  const isApiKeysPage = () => props.section === "apiKeys";
  const isReleasesPage = () => props.section === "releases";
  const isViewingReleaseSnapshot = () => isParametersPage() && !!viewedReleaseVersion();
  const isAmendMode = () => isParametersPage() && !!amendSourceVersion();

  const {
    projectsQuery,
    project,
    projectId,
    activeEnvName,
    setProjectActiveEnvName,
    environmentsQuery,
    activeEnvironment,
    activeEnvironmentKey,
    canManageProject
  } = useProjectContext();

  const configQuery = useQuery(() => ({
    queryKey: projectKeys.configEntries(params.slug, activeEnvName()),
    queryFn: () => configEntryService.getAll(projectId(), activeEnvName()),
    enabled:
      !!project() &&
      !!activeEnvName() &&
      isParametersPage() &&
      !isViewingReleaseSnapshot() &&
      !isAmendMode()
  }));

  const releasesQuery = useQuery(() => ({
    queryKey: projectKeys.configReleases(params.slug, activeEnvName()),
    queryFn: () => configReleaseService.getAll(projectId(), activeEnvName()),
    enabled: !!project() && !!activeEnvName() && isReleasesPage()
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

  // Loads the release being amended so its parameters can seed the editable buffer.
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

  const sharedLinksQuery = useQuery(() => ({
    queryKey: projectKeys.environmentShareLinks(params.slug, activeEnvName()),
    queryFn: async () => {
      const entries = await configEntryService.getAll(projectId(), activeEnvName());
      const shareLinksByEntry = await Promise.all(
        entries.map(entry =>
          configEntryService.listShareLinks(projectId(), activeEnvName(), entry.key)
        )
      );

      return shareLinksByEntry
        .flat()
        .sort(
          (left, right) =>
            new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime()
        );
    },
    enabled: !!project() && !!activeEnvName() && isSharedLinksPage()
  }));

  const configHistoryQuery = useQuery(() => ({
    queryKey: projectKeys.configEntryHistory(params.slug, activeEnvName(), editHistoryQueryKey()),
    queryFn: () => configEntryService.history(projectId(), activeEnvName(), editHistoryQueryKey()),
    enabled:
      !!project() &&
      !!activeEnvName() &&
      !!editHistoryQueryKey() &&
      isParametersPage() &&
      !isViewingReleaseSnapshot() &&
      !isAmendMode(),
    staleTime: 60_000
  }));

  const shareLinksQuery = useQuery(() => ({
    queryKey: projectKeys.configEntryShareLinks(params.slug, activeEnvName(), shareLinksQueryKey()),
    queryFn: () => configEntryService.listShareLinks(projectId(), activeEnvName(), shareLinksQueryKey()),
    enabled:
      !!project() &&
      !!activeEnvName() &&
      !!shareLinksQueryKey() &&
      isParametersPage() &&
      !isViewingReleaseSnapshot() &&
      !isAmendMode(),
    staleTime: 60_000
  }));

  const normalizeContentType = (
    contentType: string
  ): ConfigEntry["contentType"] =>
    contentType === "number" ||
    contentType === "boolean" ||
    contentType === "json" ||
    contentType === "text"
      ? contentType
      : "text";

  const normalizeScope = (scope: string): ConfigEntry["scope"] =>
    scope === "client" || scope === "server" || scope === "all" ? scope : "all";

  const mapReleaseEntryToConfigEntry = (entry: ConfigReleaseEntry): ConfigEntry => ({
    project: projectId(),
    environment: activeEnvName(),
    key: entry.key,
    value: entry.value,
    contentType: normalizeContentType(entry.contentType),
    scope: normalizeScope(entry.scope),
    activeVersion: 1,
    createdAt: amendSourceQuery.data?.createdAt ?? "",
    updatedAt: amendSourceQuery.data?.createdAt ?? ""
  });

  const currentAmendIdentity = () =>
    JSON.stringify([projectId(), activeEnvName(), amendSourceVersion()]);

  const amendBufferReady = () =>
    !amendSourceQuery.isLoading && amendSeededIdentity() === currentAmendIdentity();

  const getParamMeta = (projectName: string, environmentName: string, key: string) => {
    const baseMeta = localParamMetadataService.getMeta(projectName, environmentName, key);
    if (!isAmendMode()) {
      return baseMeta;
    }

    const description = amendDescriptions()[key];
    return description === undefined ? baseMeta : { ...baseMeta, description };
  };

  const setAmendDescription = (key: string, description: string) => {
    const trimmedDescription = description.trim();

    setAmendDescriptions(current => {
      if (!trimmedDescription) {
        if (!(key in current)) {
          return current;
        }

        const next = { ...current };
        delete next[key];
        return next;
      }

      return {
        ...current,
        [key]: trimmedDescription
      };
    });
  };

  const removeAmendEntry = (key: string) => {
    setAmendEntries(current => current.filter(entry => entry.key !== key));
    setAmendDescriptions(current => {
      if (!(key in current)) {
        return current;
      }

      const next = { ...current };
      delete next[key];
      return next;
    });

    if (editingEntry()?.key === key) {
      setEditingEntry(null);
      setEditHistoryQueryKey("");
    }

    addToast(MSG.PARAM_DELETED, "success");
  };

  createEffect(() => {
    if (!isAmendMode()) {
      setAmendEntries([]);
      setAmendDescriptions({});
      setAmendSeededIdentity(null);
      return;
    }

    const identity = currentAmendIdentity();
    if (amendSeededIdentity() === identity) {
      return;
    }

    setAmendEntries([]);
    setAmendDescriptions({});
    setAmendSeededIdentity(null);
    setShowConfigForm(false);

    if (amendSourceQuery.isLoading || amendSourceQuery.status !== "success") {
      return;
    }

    setAmendEntries(amendSourceQuery.data.entries.map(mapReleaseEntryToConfigEntry));
    setAmendSeededIdentity(identity);
  });

  const parameterEntries = createMemo<ConfigEntry[]>(() => {
    if (isViewingReleaseSnapshot()) {
      const release = releaseDetailsQuery.status === "success" ? releaseDetailsQuery.data : undefined;

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

    if (isAmendMode()) {
      return amendEntries();
    }

    return configQuery.status === "success" ? (configQuery.data ?? []) : [];
  });

  const parametersLoading = createMemo(() =>
    isViewingReleaseSnapshot()
      ? releaseDetailsQuery.isLoading
      : isAmendMode()
        ? amendSourceQuery.isLoading
        : configQuery.isLoading
  );

  const apiKeysQuery = useQuery(() => ({
    queryKey: projectKeys.apiKeys(params.slug),
    queryFn: () => projectService.listApiKeys(projectId()),
    enabled: !!project() && canManageProject() && isApiKeysPage()
  }));

  const filteredConfig = createMemo(() => {
    const q = paramSearch().toLowerCase().trim();
    const data = parameterEntries();
    if (!q) return data;
    return data.filter(
      (e: ConfigEntry) =>
        e.key.toLowerCase().includes(q) ||
        e.value.toLowerCase().includes(q) ||
        getParamMeta(projectId(), activeEnvName(), e.key)
          .displayName.toLowerCase()
          .includes(q)
    );
  });

  createEffect(() => {
    const shouldAutoOpen =
      isEnvironmentsPage() &&
      environmentsQuery.status === "success" &&
      canManageProject() &&
      (environmentsQuery.data ?? []).length === 0;

    if (shouldAutoOpen && !hasAutoOpenedEnvForm()) {
      setShowEnvForm(true);
      setHasAutoOpenedEnvForm(true);
      return;
    }

    if (!shouldAutoOpen && hasAutoOpenedEnvForm()) {
      setHasAutoOpenedEnvForm(false);
    }
  });

  createEffect(() => {
    const shouldAutoOpen =
      isApiKeysPage() &&
      apiKeysQuery.status === "success" &&
      canManageProject() &&
      (apiKeysQuery.data ?? []).length === 0;

    if (shouldAutoOpen && !hasAutoOpenedApiKeyForm()) {
      setShowApiKeyForm(true);
      setHasAutoOpenedApiKeyForm(true);
      return;
    }

    if (!shouldAutoOpen && hasAutoOpenedApiKeyForm()) {
      setHasAutoOpenedApiKeyForm(false);
    }
  });

  createEffect(() => {
    const environmentKey = activeEnvironmentKey();
    const currentConfigEntries = configQuery.status === "success" ? (configQuery.data ?? []) : [];
    const hasAutoOpened = environmentKey
      ? autoOpenedConfigFormsByEnvironment()[environmentKey]
      : false;
    const shouldAutoOpen =
      isParametersPage() &&
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
    setConfirmDeleteRelease(null);
    setConfirmActivateRelease(null);
    setConfirmClearActiveRelease(false);
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

  // Close drawers on Escape
  useEscapeKey(() => {
    setEditingEntry(null);
    setEditHistoryQueryKey("");
    setSharingEntry(null);
    setShareLinksQueryKey("");
    setGeneratedShareUrl(null);
    setShowConfigForm(false);
    setShowEnvForm(false);
    setShowApiKeyForm(false);
    setShowBulkImport(false);
    setCreateVersionOpen(false);
    setConfirmDeleteRelease(null);
    setConfirmActivateRelease(null);
    setConfirmClearActiveRelease(false);
  });

  // Env creation mutation
  const createEnvMutation = useMutation(() => ({
    mutationFn: (req: CreateEnvironmentRequest) => environmentService.create(req),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.environments(params.slug) });
      setShowEnvForm(false);
      addToast(MSG.ENV_CREATED, "success");
    },
    onError: () => addToast(MSG.ENV_CREATE_FAILED, "error")
  }));

  // Env deletion mutation
  const deleteEnvMutation = useMutation(() => ({
    mutationFn: (environmentName: string) =>
      environmentService.delete(projectId(), environmentName),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.environments(params.slug) });
      addToast(MSG.ENV_DELETED, "success");
    },
    onError: () => addToast(MSG.ENV_DELETE_FAILED, "error")
  }));

  const publishReleaseMutation = useMutation(() => ({
    mutationFn: ({
      environmentName,
      request
    }: {
      environmentName: string;
      request: PublishConfigReleaseRequest;
    }) => configReleaseService.publish(projectId(), environmentName, request),
    onSuccess: (_, { environmentName }) => {
      queryClient.invalidateQueries({
        queryKey: projectKeys.configReleases(params.slug, environmentName)
      });
      queryClient.invalidateQueries({ queryKey: projectKeys.environments(params.slug) });
      addToast(MSG.RELEASE_PUBLISHED, "success");
      navigate(`/projects/${params.slug}/releases`);
    },
    onError: error => addToast(errorMessage(error, MSG.RELEASE_PUBLISH_FAILED), "error")
  }));

  const releaseVersions = createMemo(() =>
    (releasesQuery.status === "success" ? (releasesQuery.data ?? []) : []).map(
      release => release.version
    )
  );

  const nextPatchVersion = (source: string) => {
    const [major, minor] = source.split(".");
    let maxPatch = 0;
    for (const version of releaseVersions()) {
      const [vMajor, vMinor, vPatch] = version.split(".");
      if (vMajor === major && vMinor === minor) {
        const patch = Number(vPatch);
        if (Number.isFinite(patch) && patch > maxPatch) maxPatch = patch;
      }
    }
    return `${major}.${minor}.${maxPatch + 1}`;
  };

  const setActiveReleaseMutation = useMutation(() => ({
    mutationFn: (version: string | null) =>
      version
        ? configReleaseService.setActive(projectId(), activeEnvName(), { version })
        : configReleaseService.clearActive(projectId(), activeEnvName()),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: projectKeys.configReleases(params.slug, activeEnvName())
      });
      queryClient.invalidateQueries({ queryKey: projectKeys.environments(params.slug) });
      addToast(MSG.RELEASE_ACTIVATED, "success");
    },
    onError: error => addToast(errorMessage(error, MSG.RELEASE_ACTIVATE_FAILED), "error")
  }));

  const deleteReleaseMutation = useMutation(() => ({
    mutationFn: (version: string) => {
      setDeletingReleaseVersion(version);
      return configReleaseService.delete(projectId(), activeEnvName(), version);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: projectKeys.configReleases(params.slug, activeEnvName())
      });
      setConfirmDeleteRelease(null);
      addToast(MSG.RELEASE_DELETED, "success");
    },
    onError: error => addToast(errorMessage(error, MSG.RELEASE_DELETE_FAILED), "error"),
    onSettled: () => setDeletingReleaseVersion(null)
  }));

  const revokeProjectShareLinkMutation = useMutation(() => ({
    mutationFn: (link: ParameterShareLink) => {
      setRevokingShareLinkId(link.id);
      return configEntryService.revokeShareLink(
        projectId(),
        link.environment,
        link.key,
        link.id
      );
    },
    onSuccess: (_, link) => {
      queryClient.invalidateQueries({
        queryKey: projectKeys.environmentShareLinks(params.slug, activeEnvName())
      });
      queryClient.invalidateQueries({
        queryKey: projectKeys.configEntryShareLinks(params.slug, link.environment, link.key)
      });
      addToast(MSG.SHARE_LINK_REVOKED, "success");
    },
    onError: error => addToast(errorMessage(error, MSG.SHARE_LINK_REVOKE_FAILED), "error"),
    onSettled: () => setRevokingShareLinkId(null)
  }));

  // Param creation mutation
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

  // Param deletion mutation
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

  // Drawer handlers
  const handleOpenEditDrawer = (entry: ConfigEntry) => {
    if (editingEntry()?.key === entry.key) {
      setEditingEntry(null);
      setEditHistoryQueryKey("");
      return;
    }

    setEditingEntry(entry);
    setEditHistoryQueryKey("");
    const meta = getParamMeta(projectId(), activeEnvName(), entry.key);
    setEditDescription(meta.description);

    if (isViewingReleaseSnapshot() || isAmendMode()) {
      return;
    }

    requestAnimationFrame(() => {
      if (editingEntry()?.key === entry.key) {
        setEditHistoryQueryKey(entry.key);
      }
    });
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

  // Param update settings mutation
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

  // Bulk import callback
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

  const handleAmendCreate = (data: {
    key: string;
    value: string;
    contentType: CreateConfigEntryRequest["contentType"];
    scope: CreateConfigEntryRequest["scope"];
    description: string;
  }) => {
    setAmendEntries(current => [
      ...current,
      {
        project: projectId(),
        environment: activeEnvName(),
        key: data.key,
        value: data.value,
        contentType: data.contentType,
        scope: data.scope,
        activeVersion: 1,
        createdAt: "",
        updatedAt: ""
      }
    ]);
    setAmendDescription(data.key, data.description);
    setShowConfigForm(false);
    addToast(MSG.PARAM_CREATED, "success");
  };

  const handleAmendUpdate = (data: { value: string; description: string }) => {
    const currentEntry = editingEntry();
    if (!currentEntry) {
      return;
    }

    const trimmedValue = data.value.trim();
    const trimmedDescription = data.description.trim();

    setAmendEntries(current =>
      current.map(entry =>
        entry.key === currentEntry.key
          ? {
              ...entry,
              value: trimmedValue
            }
          : entry
      )
    );
    setAmendDescription(currentEntry.key, trimmedDescription);
    setEditDescription(trimmedDescription);
    setEditingEntry(null);
    addToast(MSG.PARAM_UPDATED, "success");
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
      }
      addToast(MSG.SHARE_LINK_REVOKED, "success");
    },
    onError: error => addToast(errorMessage(error, MSG.SHARE_LINK_REVOKE_FAILED), "error"),
    onSettled: () => setRevokingShareLinkId(null)
  }));

  const createApiKeyMutation = useMutation(() => ({
    mutationFn: (data: CreateApiKeyRequest) => projectService.createApiKey(projectId(), data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.apiKeys(params.slug) });
      setShowApiKeyForm(false);
      addToast(MSG.API_KEY_CREATED, "success");
    },
    onError: () => addToast(MSG.API_KEY_CREATE_FAILED, "error")
  }));

  const deleteApiKeyMutation = useMutation(() => ({
    mutationFn: (apiKeyId: string) => projectService.deleteApiKey(projectId(), apiKeyId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.apiKeys(params.slug) });
      addToast(MSG.API_KEY_DELETED, "success");
    },
    onError: () => addToast(MSG.API_KEY_DELETE_FAILED, "error"),
    onSettled: () => setDeletingApiKeyId(null)
  }));

  return (
    <>
      <Title>
        {project()
          ? `${project()!.name}${
              isEnvironmentsPage()
                ? " Environments"
                : isSharedLinksPage()
                  ? " Shared Links"
                : isApiKeysPage()
                  ? " API Keys"
                  : isReleasesPage()
                    ? " Releases"
                    : ""
            } | Nona Config Admin`
          : "Project | Nona Config Admin"}
      </Title>
      <div class="space-y-6">
        <Show when={!projectsQuery.isLoading} fallback={<ProjectPageSkeleton />}>
          <Show
            when={project()}
            fallback={
              <div class="flex items-center justify-between gap-4">
                <div>
                  <h2 class="font-headline text-on-surface text-[17px] font-bold tracking-tight">
                    Projects
                  </h2>
                </div>
              </div>
            }
          >
            <Show when={isApiKeysPage() && canManageProject()}>
              <ProjectApiKeys
                apiKeys={apiKeysQuery.status === "success" ? (apiKeysQuery.data ?? []) : []}
                isLoading={apiKeysQuery.isLoading}
                isCreating={createApiKeyMutation.isPending}
                deletingId={deletingApiKeyId()}
                canManage={canManageProject()}
                showCreateForm={showApiKeyForm()}
                setShowCreateForm={setShowApiKeyForm}
                onCreate={data => createApiKeyMutation.mutate(data)}
                onDelete={apiKeyId => {
                  setDeletingApiKeyId(apiKeyId);
                  deleteApiKeyMutation.mutate(apiKeyId);
                }}
                onCopied={msg => addToast(msg, "success")}
              />
            </Show>

            <Show when={isEnvironmentsPage()}>
              <ProjectEnvironments
                environments={
                  environmentsQuery.status === "success" ? (environmentsQuery.data ?? []) : []
                }
                activeEnvName={activeEnvName()}
                setActiveEnvName={setProjectActiveEnvName}
                onCreateEnv={(name: string) =>
                  createEnvMutation.mutate({ projectId: projectId(), name })
                }
                onDeleteEnv={setConfirmDeleteEnv}
                showEnvForm={showEnvForm()}
                setShowEnvForm={setShowEnvForm}
                createEnvPending={createEnvMutation.isPending}
                canManage={canManageProject()}
              />
            </Show>

            <Show when={isReleasesPage()}>
              <Show
                when={activeEnvName()}
                fallback={
                  <div class="border-outline-variant/15 bg-surface-container-low rounded-2xl border px-5 py-6 text-sm text-on-surface-variant">
                    Select an active environment from the header to manage releases.
                  </div>
                }
              >
                <ProjectReleases
                  environmentName={activeEnvName()}
                  activeReleaseVersion={activeEnvironment()?.activeReleaseVersion}
                  releases={releasesQuery.status === "success" ? (releasesQuery.data ?? []) : []}
                  isLoading={releasesQuery.isLoading}
                  isActivating={setActiveReleaseMutation.isPending}
                  amendingVersion={null}
                  deletingVersion={deletingReleaseVersion()}
                  canManage={canManageProject()}
                  onCreateVersion={() => setCreateVersionOpen(true)}
                  onView={version =>
                    navigate(`/projects/${params.slug}?viewRelease=${encodeURIComponent(version)}`)
                  }
                  onAmend={version =>
                    navigate(
                      `/projects/${params.slug}?release=${encodeURIComponent(nextPatchVersion(version))}&amend=${encodeURIComponent(version)}`
                    )
                  }
                  onActivate={setConfirmActivateRelease}
                  onClearActive={() => setConfirmClearActiveRelease(true)}
                  onDelete={setConfirmDeleteRelease}
                />
              </Show>
            </Show>

            <Show when={isSharedLinksPage()}>
              <Show
                when={activeEnvName()}
                fallback={
                  <div class="border-outline-variant/15 bg-surface-container-low rounded-2xl border px-5 py-6 text-sm text-on-surface-variant">
                    Select an active environment from the header to view shared links.
                  </div>
                }
              >
                <ProjectShareLinks
                  environmentName={activeEnvName()}
                  shareLinks={sharedLinksQuery.status === "success" ? (sharedLinksQuery.data ?? []) : []}
                  isLoading={sharedLinksQuery.isLoading}
                  revokingId={revokingShareLinkId()}
                  canManage={canManageProject()}
                  onCopy={copyShareUrl}
                  onRevoke={link => revokeProjectShareLinkMutation.mutate(link)}
                  buildShareUrl={buildShareUrl}
                />
              </Show>
            </Show>

            <Show when={isParametersPage()}>
              <Show when={isViewingReleaseSnapshot()}>
                <div
                  data-testid="release-view-banner"
                  class="border-secondary/25 bg-secondary/5 animate-fade-in flex flex-col gap-3 rounded-2xl border p-4 sm:flex-row sm:items-center sm:justify-between"
                >
                  <div class="flex items-center gap-2 text-[13px]">
                    <span class="material-symbols-outlined text-secondary text-[18px]">
                      visibility
                    </span>
                    <span class="text-on-surface-variant">
                      Viewing release{" "}
                      <span class="text-secondary font-mono font-bold">{viewedReleaseVersion()}</span>
                      {" "}for {activeEnvName()}.
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
                <div
                  data-testid="release-amend-banner"
                  class="border-primary/25 bg-primary/5 animate-fade-in flex flex-col gap-3 rounded-2xl border p-4 sm:flex-row sm:items-center sm:justify-between"
                >
                  <div class="flex items-center gap-2 text-[13px]">
                    <span class="material-symbols-outlined text-primary text-[18px]">edit</span>
                    <span class="text-on-surface-variant">
                      Amending <span class="text-on-surface font-mono font-bold">{amendSourceVersion()}</span>
                      {" "}into patch <span class="text-primary font-mono font-bold">{releaseDraftVersion()}</span>
                      {" "}using the full parameters editor.
                    </span>
                  </div>
                  <div class="flex shrink-0 flex-wrap justify-end gap-2">
                    <button
                      data-testid="release-amend-confirm-button"
                      type="button"
                      disabled={publishReleaseMutation.isPending || !activeEnvName() || !amendBufferReady()}
                      onClick={() =>
                        publishReleaseMutation.mutate({
                          environmentName: activeEnvName(),
                          request: {
                            version: releaseDraftVersion() ?? "",
                            makeActive: false,
                            entries: amendEntries().map(entry => ({
                              key: entry.key,
                              value: entry.value,
                              contentType: entry.contentType,
                              scope: entry.scope
                            }))
                          }
                        })
                      }
                      class="bg-primary text-on-primary inline-flex h-9 cursor-pointer items-center gap-1.5 rounded-lg border-0 px-4 text-[12px] font-semibold transition-all hover:brightness-105 active:scale-[0.98] disabled:opacity-50"
                    >
                      <span class="material-symbols-outlined text-[16px]">check</span>
                      {publishReleaseMutation.isPending ? "Creating…" : "Create release"}
                    </button>
                    <button
                      data-testid="release-amend-cancel-button"
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
                      <span class="text-primary font-mono font-bold">{releaseDraftVersion()}</span> —
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
                      {publishReleaseMutation.isPending ? "Creating…" : "Create release"}
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
              <ProjectParamsTab
                activeEnvName={activeEnvName()}
                configEntries={parameterEntries()}
                filteredConfig={filteredConfig()}
                isLoading={parametersLoading()}
                paramSearch={paramSearch()}
                onParamSearch={setParamSearch}
                onToggleBulkImport={() => {
                  if (isAmendMode()) return;
                  setShowBulkImport(!showBulkImport());
                  setShowConfigForm(false);
                }}
                onToggleConfigForm={() => {
                  setShowConfigForm(!showConfigForm());
                  setShowBulkImport(false);
                }}
                showConfigForm={showConfigForm()}
                showBulkImportButton={!isAmendMode()}
                bulkImportPanel={
                  !isViewingReleaseSnapshot() && !isAmendMode() && canManageProject() && showBulkImport() ? (
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
                  onCancel: () => setShowConfigForm(false),
                  onSubmit: data => {
                    if (!canManageProject()) return;
                    if (isAmendMode()) {
                      handleAmendCreate(data);
                      return;
                    }

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
                  isPending: isAmendMode() ? false : createConfigMutation.isPending
                }}
                table={{
                  isLoading: parametersLoading(),
                  projectId: projectId(),
                  activeEnvName: activeEnvName(),
                  filteredConfig: filteredConfig(),
                  editingEntry: editingEntry(),
                  onSelectEntry: handleOpenEditDrawer,
                  onShareEntry: handleOpenShareDialog,
                  showShareActions: !isAmendMode(),
                  onDeleteEntry: setConfirmDeleteEntry,
                  canManage: canManageProject() && !isViewingReleaseSnapshot(),
                  copiedKey: copiedKey(),
                  onCopyValue: copyValue,
                  getParamMeta,
                  initialDescription: editDescription(),
                  onCloseEntry: () => {
                    setEditingEntry(null);
                    setEditHistoryQueryKey("");
                  },
                  onSaveSettings: data => {
                    if (!canManageProject()) return;
                    if (isAmendMode()) {
                      handleAmendUpdate(data);
                      return;
                    }

                    setEditDescription(data.description);
                    updateConfigMutation.mutate({
                      key: editingEntry()!.key,
                      value: data.value,
                      contentType: editingEntry()!.contentType,
                      scope: editingEntry()!.scope,
                      description: data.description
                    });
                  },
                  isSaving: isAmendMode() ? false : updateConfigMutation.isPending,
                  historyVersions:
                    !isAmendMode() && configHistoryQuery.status === "success"
                      ? (configHistoryQuery.data ?? [])
                      : [],
                  isHistoryLoading: !isAmendMode() && configHistoryQuery.isLoading,
                  isRollingBack: !isAmendMode() && rollbackConfigMutation.isPending,
                  onRollbackVersion: handleRollbackVersion,
                  search: paramSearch(),
                  isReadOnly: isViewingReleaseSnapshot(),
                  releaseVersion: viewedReleaseVersion()
                }}
              />

              <ParameterShareDialog
                entry={sharingEntry()}
                shareLinks={shareLinksQuery.status === "success" ? (shareLinksQuery.data ?? []) : []}
                generatedUrl={generatedShareUrl()}
                isLoading={shareLinksQuery.isLoading}
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
            </Show>
          </Show>
        </Show>
      </div>

      <Show when={isParametersPage()}>
        <ConfirmDialog
          open={confirmDeleteEntry() !== null}
          title={isAmendMode() ? "Remove Parameter?" : "Delete Parameter?"}
          message={
            isAmendMode() ? (
              <>
                Remove <span class="text-primary font-mono font-bold">{confirmDeleteEntry()}</span>
                {" "}from the amend buffer for the <span class="text-on-surface font-medium">{activeEnvName()}</span>
                {" "}environment? The live parameters will stay unchanged until you create the release.
              </>
            ) : (
              <>
                Permanently delete{" "}
                <span class="text-primary font-mono font-bold">{confirmDeleteEntry()}</span> from the{" "}
                <span class="text-on-surface font-medium">{activeEnvName()}</span> environment?
              </>
            )
          }
          confirmLabel={isAmendMode() ? "Remove Parameter" : "Delete Parameter"}
          variant="danger"
          isLoading={isAmendMode() ? false : deleteConfigMutation.isPending}
          onConfirm={() => {
            const key = confirmDeleteEntry();
            if (key) {
              if (isAmendMode()) {
                removeAmendEntry(key);
              } else {
                deleteConfigMutation.mutate(key);
              }
              setConfirmDeleteEntry(null);
            }
          }}
          onCancel={() => setConfirmDeleteEntry(null)}
          testId="delete-parameter-dialog"
          confirmTestId="delete-parameter-confirm-button"
          cancelTestId="delete-parameter-cancel-button"
        />
      </Show>

      <ConfirmDialog
        open={confirmDeleteEnv() !== null}
        title="Delete Environment?"
        message={
          <>
            Permanently delete the{" "}
            <span class="text-primary font-mono font-bold">{confirmDeleteEnv()}</span> environment
            and all its parameters?
          </>
        }
        confirmLabel="Delete Environment"
        variant="danger"
        isLoading={deleteEnvMutation.isPending}
        onConfirm={() => {
          const env = confirmDeleteEnv();
          if (env) {
            deleteEnvMutation.mutate(env);
            setConfirmDeleteEnv(null);
          }
        }}
        onCancel={() => setConfirmDeleteEnv(null)}
        testId="delete-environment-dialog"
        confirmTestId="delete-environment-confirm-button"
        cancelTestId="delete-environment-cancel-button"
      />

      <Show when={isReleasesPage()}>
        <ConfirmDialog
          open={confirmActivateRelease() !== null}
          title="Activate Release?"
          message={
            <>
              Make release{" "}
              <span class="text-primary font-mono font-bold">{confirmActivateRelease()}</span> the
              active version for the{" "}
              <span class="text-on-surface font-medium">{activeEnvName()}</span> environment?
            </>
          }
          confirmLabel="Activate Release"
          isLoading={setActiveReleaseMutation.isPending}
          onConfirm={() => {
            const version = confirmActivateRelease();
            if (version) {
              setActiveReleaseMutation.mutate(version);
              setConfirmActivateRelease(null);
            }
          }}
          onCancel={() => setConfirmActivateRelease(null)}
          testId="release-activate-dialog"
          confirmTestId="release-activate-confirm-button"
          cancelTestId="release-activate-cancel-button"
        />

        <ConfirmDialog
          open={confirmClearActiveRelease()}
          title="Clear Active Release?"
          message={
            <>
              Remove the active release from the{" "}
              <span class="text-on-surface font-medium">{activeEnvName()}</span> environment? Clients
              will no longer resolve to a pinned release until another one is activated.
            </>
          }
          confirmLabel="Clear Active Release"
          isLoading={setActiveReleaseMutation.isPending}
          onConfirm={() => {
            setActiveReleaseMutation.mutate(null);
            setConfirmClearActiveRelease(false);
          }}
          onCancel={() => setConfirmClearActiveRelease(false)}
          testId="release-clear-active-dialog"
          confirmTestId="release-clear-active-confirm-button"
          cancelTestId="release-clear-active-cancel-button"
        />

        <ConfirmDialog
          open={confirmDeleteRelease() !== null}
          title="Delete Release?"
          message={
            <>
              Permanently delete release{" "}
              <span class="text-primary font-mono font-bold">{confirmDeleteRelease()}</span> from the{" "}
              <span class="text-on-surface font-medium">{activeEnvName()}</span> environment? The
              working configuration will not be changed.
            </>
          }
          confirmLabel="Delete Release"
          variant="danger"
          isLoading={deleteReleaseMutation.isPending}
          onConfirm={() => {
            const version = confirmDeleteRelease();
            if (version) {
              deleteReleaseMutation.mutate(version);
            }
          }}
          onCancel={() => setConfirmDeleteRelease(null)}
          testId="release-delete-dialog"
          confirmTestId="release-delete-confirm-button"
          cancelTestId="release-delete-cancel-button"
        />

        <ReleaseVersionDialog
          open={createVersionOpen()}
          title="Create a version"
          description="Choose a major and minor version. The release will start at patch .0 after you review its parameters."
          initialVersion=""
          existingVersions={releaseVersions()}
          confirmLabel="Continue to parameters"
          placeholder="1.2"
          validationMessage="Use major.minor."
          versionFormat="majorMinor"
          normalizeVersion={version => `${version}.0`}
          onConfirm={version => {
            setCreateVersionOpen(false);
            navigate(`/projects/${params.slug}?release=${encodeURIComponent(version)}`);
          }}
          onCancel={() => setCreateVersionOpen(false)}
        />
      </Show>
    </>
  );
}
