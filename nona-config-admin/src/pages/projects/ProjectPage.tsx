import { writeClipboard } from "@solid-primitives/clipboard";
import { createTimer } from "@solid-primitives/timer";
import { Title } from "@solidjs/meta";
import { useParams } from "@solidjs/router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/solid-query";
import { Show, createEffect, createMemo, createSignal } from "solid-js";

import { configEntryService } from "../../entities/project/api/config-entry.service";
import { environmentService } from "../../entities/project/api/environment.service";
import { projectService } from "../../entities/project/api/project.service";
import { ConfirmDialog } from "../../shared/ui/confirm-dialog";
import { useToast } from "../../shared/ui/toast";

import type { ParsedImport } from "../../features/project-bulk-import/ProjectBulkImport";
import { ProjectBulkImport } from "../../features/project-bulk-import/ProjectBulkImport";
import { ProjectEnvironments } from "../../features/project-environments/ProjectEnvironments";
import { ProjectParamEditDrawer } from "../../features/project-param-edit/ProjectParamEditDrawer";
import { ParameterShareDialog } from "../../features/project-param-share/ParameterShareDialog";
import { ProjectParamsTab } from "../../features/project-params/ProjectParamsTab";
import { ProjectApiKeys } from "./components/ProjectApiKeys";
import { ProjectHeader } from "./components/ProjectHeader";
import { ProjectPageSkeleton } from "./components/ProjectPageSkeleton";

import { canManageProjectResources } from "../../entities/auth/model/permissions";
import {
  autoFormatKey,
  localParamMetadataService
} from "../../entities/project/api/metadata.service";
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
  Project
} from "../../types";

const errorMessage = (caught: unknown, fallback: string) =>
  caught instanceof Error && caught.message ? caught.message : fallback;

export default function ProjectPage() {
  const params = useParams<{ slug: string }>();
  const queryClient = useQueryClient();
  const { addToast } = useToast();

  const [activeEnvName, setActiveEnvName] = createSignal<string>("");
  const [paramSearch, setParamSearch] = createSignal("");
  const [copiedKey, setCopiedKey] = createSignal<string | null>(null);
  const [showEnvForm, setShowEnvForm] = createSignal(false);
  const [showConfigForm, setShowConfigForm] = createSignal(false);
  const [confirmDeleteEntry, setConfirmDeleteEntry] = createSignal<string | null>(null);
  const [confirmDeleteEnv, setConfirmDeleteEnv] = createSignal<string | null>(null);
  const [editingEntry, setEditingEntry] = createSignal<ConfigEntry | null>(null);
  const [sharingEntry, setSharingEntry] = createSignal<ConfigEntry | null>(null);
  const [generatedShareUrl, setGeneratedShareUrl] = createSignal<string | null>(null);
  const [revokingShareLinkId, setRevokingShareLinkId] = createSignal<number | null>(null);
  const [editDisplayName, setEditDisplayName] = createSignal("");
  const [editDescription, setEditDescription] = createSignal("");
  const [showBulkImport, setShowBulkImport] = createSignal(false);
  const [deletingApiKeyId, setDeletingApiKeyId] = createSignal<string | null>(null);

  createTimer(
    () => setCopiedKey(null),
    () => (copiedKey() ? 1500 : false),
    setTimeout
  );

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

  const environmentsQuery = useQuery(() => ({
    queryKey: projectKeys.environments(params.slug),
    queryFn: () => environmentService.getAll(projectId()),
    enabled: !!project()
  }));

  createEffect(() => {
    const envs = environmentsQuery.status === "success" ? environmentsQuery.data : undefined;
    if (envs && envs.length > 0 && !activeEnvName()) {
      setActiveEnvName(envs[0].name);
    }
  });

  const configQuery = useQuery(() => ({
    queryKey: projectKeys.configEntries(params.slug, activeEnvName()),
    queryFn: () => configEntryService.getAll(projectId(), activeEnvName()),
    enabled: !!project() && !!activeEnvName()
  }));

  const editingEntryKey = createMemo(() => editingEntry()?.key ?? "");
  const sharingEntryKey = createMemo(() => sharingEntry()?.key ?? "");

  const configHistoryQuery = useQuery(() => ({
    queryKey: projectKeys.configEntryHistory(params.slug, activeEnvName(), editingEntryKey()),
    queryFn: () => configEntryService.history(projectId(), activeEnvName(), editingEntryKey()),
    enabled: !!project() && !!activeEnvName() && !!editingEntryKey()
  }));

  const shareLinksQuery = useQuery(() => ({
    queryKey: projectKeys.configEntryShareLinks(params.slug, activeEnvName(), sharingEntryKey()),
    queryFn: () => configEntryService.listShareLinks(projectId(), activeEnvName(), sharingEntryKey()),
    enabled: !!project() && !!activeEnvName() && !!sharingEntryKey()
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
    enabled: !!project() && canManageProject()
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

  // Close drawers on Escape
  useEscapeKey(() => {
    setEditingEntry(null);
    setSharingEntry(null);
    setGeneratedShareUrl(null);
    setShowConfigForm(false);
    setShowEnvForm(false);
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
    setEditingEntry(entry);
    const meta = localParamMetadataService.getMeta(projectId(), activeEnvName(), entry.key);
    setEditDisplayName(meta.displayName);
    setEditDescription(meta.description);
  };

  const handleOpenShareDialog = (entry: ConfigEntry) => {
    setSharingEntry(entry);
    setGeneratedShareUrl(null);
  };

  // Param update settings mutation
  const updateConfigMutation = useMutation(() => ({
    mutationFn: (req: {
      key: string;
      value: string;
      contentType: ConfigEntry["contentType"];
      scope: ConfigEntry["scope"];
      displayName?: string;
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

      const dispName =
        variables.displayName !== undefined
          ? variables.displayName.trim()
          : editDisplayName().trim();
      const desc =
        variables.description !== undefined
          ? variables.description.trim()
          : editDescription().trim();

      if (editingEntry()) {
        localParamMetadataService.setMeta(projectId(), activeEnvName(), editingEntry()!.key, {
          displayName: dispName,
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
      const dispName = autoFormatKey(item.key);
      const desc = `Imported parameter: ${item.key}`;

      localParamMetadataService.setMeta(projectId(), activeEnvName(), item.key, {
        displayName: dispName,
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
      setGeneratedShareUrl(`${window.location.origin}/share/${shareLink.token}`);
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
        {project() ? `${project()!.name} | Nona Config Admin` : "Project | Nona Config Admin"}
      </Title>
      <div class="space-y-6">
        <Show when={!projectsQuery.isLoading} fallback={<ProjectPageSkeleton />}>
          <ProjectHeader
            project={project()}
            showConfigForm={showConfigForm()}
            showBulkImport={showBulkImport()}
            showEnvForm={showEnvForm()}
            canManageProject={canManageProject()}
            onToggleEnvForm={() => setShowEnvForm(!showEnvForm())}
            onToggleBulkImport={() => {
              setShowBulkImport(!showBulkImport());
              setShowConfigForm(false);
            }}
            onToggleConfigForm={() => {
              setShowConfigForm(!showConfigForm());
              setShowBulkImport(false);
            }}
          />

          <Show when={project() && canManageProject()}>
            <ProjectApiKeys
              apiKeys={apiKeysQuery.status === "success" ? (apiKeysQuery.data ?? []) : []}
              environments={
                environmentsQuery.status === "success" ? (environmentsQuery.data ?? []) : []
              }
              isLoading={apiKeysQuery.isLoading}
              isCreating={createApiKeyMutation.isPending}
              deletingId={deletingApiKeyId()}
              canManage={canManageProject()}
              onCreate={data => createApiKeyMutation.mutate(data)}
              onDelete={apiKeyId => {
                setDeletingApiKeyId(apiKeyId);
                deleteApiKeyMutation.mutate(apiKeyId);
              }}
              onCopied={msg => addToast(msg, "success")}
            />
          </Show>

          <Show when={canManageProject() && showBulkImport()}>
            <ProjectBulkImport
              onCancel={() => setShowBulkImport(false)}
              onImport={handleBulkImport}
              existingEntries={configQuery.status === "success" ? (configQuery.data ?? []) : []}
              isPending={updateConfigMutation.isPending}
              addToast={addToast}
            />
          </Show>

          <ProjectEnvironments
            environments={
              environmentsQuery.status === "success" ? (environmentsQuery.data ?? []) : []
            }
            activeEnvName={activeEnvName()}
            setActiveEnvName={setActiveEnvName}
            onCreateEnv={(name: string) =>
              createEnvMutation.mutate({ projectId: projectId(), name })
            }
            onDeleteEnv={setConfirmDeleteEnv}
            showEnvForm={showEnvForm()}
            setShowEnvForm={setShowEnvForm}
            createEnvPending={createEnvMutation.isPending}
            canManage={canManageProject()}
          />

          <ProjectParamsTab
            activeEnvName={activeEnvName()}
            environments={
              environmentsQuery.status === "success" ? (environmentsQuery.data ?? []) : []
            }
            filteredConfig={filteredConfig()}
            isLoading={configQuery.isLoading}
            projectId={projectId()}
            paramSearch={paramSearch()}
            onParamSearch={setParamSearch}
            showConfigForm={showConfigForm()}
            onCancelCreate={() => setShowConfigForm(false)}
            onSubmitCreate={data => {
              if (!canManageProject()) return;
              localParamMetadataService.setMeta(projectId(), activeEnvName(), data.key, {
                displayName: data.displayName,
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
          />

          <ProjectParamEditDrawer
            entry={editingEntry()}
            activeEnvName={activeEnvName()}
            initialDisplayName={editDisplayName()}
            initialDescription={editDescription()}
            onClose={() => setEditingEntry(null)}
            onSaveSettings={data => {
              if (!canManageProject()) return;
              setEditDisplayName(data.displayName);
              setEditDescription(data.description);
              updateConfigMutation.mutate({
                key: editingEntry()!.key,
                value: data.value,
                contentType: editingEntry()!.contentType,
                scope: editingEntry()!.scope,
                displayName: data.displayName,
                description: data.description
              });
            }}
            isSaving={updateConfigMutation.isPending}
            canManage={canManageProject()}
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
              setGeneratedShareUrl(null);
            }}
            onCreate={data => createShareLinkMutation.mutate(data)}
            onRevoke={shareLinkId => {
              setRevokingShareLinkId(shareLinkId);
              revokeShareLinkMutation.mutate(shareLinkId);
            }}
            onCopy={copyShareUrl}
          />
        </Show>
      </div>

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
    </>
  );
}
