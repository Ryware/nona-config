import { useParams } from "@solidjs/router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/solid-query";
import { Show, createEffect, createSignal } from "solid-js";

import { projectService } from "../../../entities/project/api/project.service";
import { projectKeys } from "../../../entities/project/queries/keys";
import { useEscapeKey } from "../../../shared/hooks/useEscapeKey";
import { MSG } from "../../../shared/lib/messages";
import { useToast } from "../../../shared/ui/toast";
import type { CreateApiKeyRequest } from "../../../types";
import { ProjectApiKeys } from "../components/ProjectApiKeys";
import { ProjectSectionLayout } from "../components/ProjectSectionLayout";
import { useProjectContext } from "../hooks/useProjectContext";

export default function ApiKeysSection() {
  const params = useParams<{ slug: string }>();
  const queryClient = useQueryClient();
  const { addToast } = useToast();
  const [showApiKeyForm, setShowApiKeyForm] = createSignal(false);
  const [hasAutoOpenedApiKeyForm, setHasAutoOpenedApiKeyForm] = createSignal(false);
  const [deletingApiKeyId, setDeletingApiKeyId] = createSignal<string | null>(null);

  const { projectsQuery, project, projectId, activeEnvName, canManageProject } =
    useProjectContext();

  const apiKeysQuery = useQuery(() => ({
    queryKey: projectKeys.apiKeys(params.slug),
    queryFn: () => projectService.listApiKeys(projectId()),
    enabled: !!project() && canManageProject()
  }));

  createEffect(() => {
    const shouldAutoOpen =
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

  useEscapeKey(() => {
    setShowApiKeyForm(false);
  });

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
    <ProjectSectionLayout
      section="apiKeys"
      project={project()}
      projectLoading={projectsQuery.isLoading}
    >
      <Show when={canManageProject()}>
        <ProjectApiKeys
          apiKeys={apiKeysQuery.status === "success" ? (apiKeysQuery.data ?? []) : []}
          isLoading={apiKeysQuery.isLoading}
          isCreating={createApiKeyMutation.isPending}
          deletingId={deletingApiKeyId()}
          canManage={canManageProject()}
          activeEnvironmentName={activeEnvName()}
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
    </ProjectSectionLayout>
  );
}
