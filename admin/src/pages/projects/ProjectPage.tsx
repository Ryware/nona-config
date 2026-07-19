import { writeClipboard } from "@solid-primitives/clipboard";
import { createTimer } from "@solid-primitives/timer";
import { Title } from "@solidjs/meta";
import { useParams } from "@solidjs/router";
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
import { ProjectShareLinks } from "../../features/project-share-links/ProjectShareLinks";
import { ProjectApiKeys } from "./components/ProjectApiKeys";
import { ProjectPageSkeleton } from "./components/ProjectPageSkeleton";

import { canManageProjectResources } from "../../entities/auth/model/permissions";
import {
  localParamMetadataService
} from "../../entities/project/api/metadata.service";
import {
  getActiveEnvironmentName,
  setActiveEnvironmentName,
  syncActiveEnvironment,
} from "../../entities/project/model/active-environment";
import { setActiveProjectSlug } from "../../entities/project/model/active-project";
import { projectKeys } from "../../entities/project/queries/keys";
import { userService } from "../../entities/user/api/user.service";
import { userKeys } from "../../entities/user/queries/keys";
import { useEscapeKey } from "../../shared/hooks/useEscapeKey";
import { MSG } from "../../shared/lib/messages";
import type {
  ConfigEntry,
  ConfigEntryVersion,
  CreateParameterShareLinkRequest,
  CreateConfigEntryRequest,
  CreateApiKeyRequest,
  CreateEnvironmentRequest,
  ParameterShareLink,
  Project
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
  const queryClient = useQueryClient();
  const { addToast } = useToast();

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
  const [confirmDraftRelease, setConfirmDraftRelease] = createSignal<string | null>(null);
  const [draftingReleaseVersion, setDraftingReleaseVersion] = createSignal<string | null>(null);
  const [confirmDeleteRelease, setConfirmDeleteRelease] = createSignal<string | null>(null);
  const [deletingReleaseVersion, setDeletingReleaseVersion] = createSignal<string | null>(null);

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

  const projectsQuery = useQuery(() => ({
    queryKey: projectKeys.list(),
    queryFn: () => projectService.getAll()
  }));

  const project = createMemo(() =>
    projectsQuery.status === "success"
      ? projectsQuery.data?.find((p: Project) => p.urlSlug === params.slug)
      : undefined
  );

  const projectId = createMemo(() => project()?.name ?? "");

  createEffect(() => {
    if (project()) {
      setActiveProjectSlug(project()!.urlSlug);
    }
  });

  const activeEnvName = createMemo(() =>
    project() ? getActiveEnvironmentName(project()!.urlSlug) : ""
  );

  const setProjectActiveEnvName = (environmentName: string) => {
    const currentProject = project();
    if (!currentProject) {
      return;
    }

    setActiveEnvironmentName(currentProject.urlSlug, environmentName);
  };

  const environmentsQuery = useQuery(() => ({
    queryKey: projectKeys.environments(params.slug),
    queryFn: () => environmentService.getAll(projectId()),
    enabled: !!project()
  }));

  createEffect(() => {
    const currentProject = project();
    const environments =
      environmentsQuery.status === "success" ? (environmentsQuery.data ?? []) : undefined;

    if (currentProject && environments) {
      syncActiveEnvironment(currentProject.urlSlug, environments);
    }
  });

  const configQuery = useQuery(() => ({
    queryKey: projectKeys.configEntries(params.slug, activeEnvName()),
    queryFn: () => configEntryService.getAll(projectId(), activeEnvName()),
    enabled: !!project() && !!activeEnvName() && isParametersPage()
  }));

  const releasesQuery = useQuery(() => ({
    queryKey: projectKeys.configReleases(params.slug, activeEnvName()),
    queryFn: () => configReleaseService.getAll(projectId(), activeEnvName()),
    enabled: !!project() && !!activeEnvName() && isReleasesPage()
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

  const activeEnvironment = createMemo(() => {
    const envs = environmentsQuery.status === "success" ? environmentsQuery.data ?? [] : [];
    return envs.find(env => env.name === activeEnvName());
  });
  const activeEnvironmentKey = createMemo(() =>
    project() && activeEnvName() ? `${project()!.urlSlug}:${activeEnvName()}` : ""
  );

  const configHistoryQuery = useQuery(() => ({
    queryKey: projectKeys.configEntryHistory(params.slug, activeEnvName(), editHistoryQueryKey()),
    queryFn: () => configEntryService.history(projectId(), activeEnvName(), editHistoryQueryKey()),
    enabled: !!project() && !!activeEnvName() && !!editHistoryQueryKey() && isParametersPage(),
    staleTime: 60_000
  }));

  const shareLinksQuery = useQuery(() => ({
    queryKey: projectKeys.configEntryShareLinks(params.slug, activeEnvName(), shareLinksQueryKey()),
    queryFn: () => configEntryService.listShareLinks(projectId(), activeEnvName(), shareLinksQueryKey()),
    enabled: !!project() && !!activeEnvName() && !!shareLinksQueryKey() && isParametersPage(),
    staleTime: 60_000
  }));

  const usersQuery = useQuery(() => ({
    queryKey: userKeys.list(),
    queryFn: () => userService.getAll(),
    enabled: !!project()
  }));

  const canManageProject = createMemo(() =>
    canManageProjectResources(projectId(), usersQuery.status === "success" ? (usersQuery.data ?? []) : [])
  );

  const apiKeysQuery = useQuery(() => ({
    queryKey: projectKeys.apiKeys(params.slug),
    queryFn: () => projectService.listApiKeys(projectId()),
    enabled: !!project() && canManageProject() && isApiKeysPage()
  }));

  const filteredConfig = createMemo(() => {
    const q = paramSearch().toLowerCase().trim();
    const data = configQuery.status === "success" ? (configQuery.data ?? []) : [];
    if (!q) return data;
    return data.filter(
      (e: ConfigEntry) =>
        e.key.toLowerCase().includes(q) ||
        e.value.toLowerCase().includes(q) ||
        localParamMetadataService
          .getMeta(projectId(), activeEnvName(), e.key)
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
    mutationFn: (req: { version: string; makeActive: boolean }) =>
      configReleaseService.publish(projectId(), activeEnvName(), req),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: projectKeys.configReleases(params.slug, activeEnvName())
      });
      queryClient.invalidateQueries({ queryKey: projectKeys.environments(params.slug) });
      addToast(MSG.RELEASE_PUBLISHED, "success");
    },
    onError: error => addToast(errorMessage(error, MSG.RELEASE_PUBLISH_FAILED), "error")
  }));

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

  const createReleaseDraftMutation = useMutation(() => ({
    mutationFn: (version: string) => {
      setDraftingReleaseVersion(version);
      return configReleaseService.createDraft(projectId(), activeEnvName(), version);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: projectKeys.configEntries(params.slug, activeEnvName())
      });
      setConfirmDraftRelease(null);
      addToast(MSG.RELEASE_DRAFT_CREATED, "success");
    },
    onError: error => addToast(errorMessage(error, MSG.RELEASE_DRAFT_FAILED), "error"),
    onSettled: () => setDraftingReleaseVersion(null)
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
    const meta = localParamMetadataService.getMeta(projectId(), activeEnvName(), entry.key);
    setEditDescription(meta.description);

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
                  isPublishing={publishReleaseMutation.isPending}
                  isActivating={setActiveReleaseMutation.isPending}
                  draftingVersion={draftingReleaseVersion()}
                  deletingVersion={deletingReleaseVersion()}
                  canManage={canManageProject()}
                  onPublish={(version, makeActive) =>
                    publishReleaseMutation.mutateAsync({ version, makeActive })
                  }
                  onActivate={version => setActiveReleaseMutation.mutate(version)}
                  onClearActive={() => setActiveReleaseMutation.mutate(null)}
                  onDraft={setConfirmDraftRelease}
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
              <ProjectParamsTab
                activeEnvName={activeEnvName()}
                environments={
                  environmentsQuery.status === "success" ? (environmentsQuery.data ?? []) : []
                }
                configEntries={configQuery.status === "success" ? (configQuery.data ?? []) : []}
                filteredConfig={filteredConfig()}
                editingEntry={editingEntry()}
                isLoading={configQuery.isLoading}
                projectId={projectId()}
                paramSearch={paramSearch()}
                onParamSearch={setParamSearch}
                onToggleBulkImport={() => {
                  setShowBulkImport(!showBulkImport());
                  setShowConfigForm(false);
                }}
                onToggleConfigForm={() => {
                  setShowConfigForm(!showConfigForm());
                  setShowBulkImport(false);
                }}
                showConfigForm={showConfigForm()}
                bulkImportPanel={
                  canManageProject() && showBulkImport() ? (
                    <ProjectBulkImport
                      onCancel={() => setShowBulkImport(false)}
                      onImport={handleBulkImport}
                      existingEntries={configQuery.status === "success" ? (configQuery.data ?? []) : []}
                      isPending={updateConfigMutation.isPending}
                      addToast={addToast}
                    />
                  ) : undefined
                }
                onCancelCreate={() => setShowConfigForm(false)}
                onSubmitCreate={data => {
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
                }}
                isCreatePending={createConfigMutation.isPending}
                onSelectEntry={handleOpenEditDrawer}
                onShareEntry={handleOpenShareDialog}
                onDeleteEntry={setConfirmDeleteEntry}
                canManage={canManageProject()}
                copiedKey={copiedKey()}
                onCopyValue={copyValue}
                getParamMeta={(proj, env, key) => localParamMetadataService.getMeta(proj, env, key)}
                initialDescription={editDescription()}
                onCloseEntry={() => {
                  setEditingEntry(null);
                  setEditHistoryQueryKey("");
                }}
                onSaveSettings={data => {
                  if (!canManageProject()) return;
                  setEditDescription(data.description);
                  updateConfigMutation.mutate({
                    key: editingEntry()!.key,
                    value: data.value,
                    contentType: editingEntry()!.contentType,
                    scope: editingEntry()!.scope,
                    description: data.description
                  });
                }}
                isSaving={updateConfigMutation.isPending}
                historyVersions={configHistoryQuery.status === "success" ? (configHistoryQuery.data ?? []) : []}
                isHistoryLoading={configHistoryQuery.isLoading}
                isRollingBack={rollbackConfigMutation.isPending}
                onRollbackVersion={handleRollbackVersion}
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
          open={confirmDraftRelease() !== null}
          title="Load Release into Workspace?"
          message={
            <>
              Load release <span class="text-primary font-mono font-bold">{confirmDraftRelease()}</span>{" "}
              into the editable working configuration for{" "}
              <span class="text-on-surface font-medium">{activeEnvName()}</span>? This replaces any
              unpublished changes, but does not change the active release or what clients receive.
            </>
          }
          confirmLabel="Load Release"
          variant="info"
          isLoading={createReleaseDraftMutation.isPending}
          onConfirm={() => {
            const version = confirmDraftRelease();
            if (version) {
              createReleaseDraftMutation.mutate(version);
            }
          }}
          onCancel={() => setConfirmDraftRelease(null)}
          testId="release-draft-dialog"
          confirmTestId="release-draft-confirm-button"
          cancelTestId="release-draft-cancel-button"
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
      </Show>
    </>
  );
}
