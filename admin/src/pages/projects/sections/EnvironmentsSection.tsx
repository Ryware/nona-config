import { useParams } from "@solidjs/router";
import { useMutation, useQueryClient } from "@tanstack/solid-query";
import { createEffect, createSignal } from "solid-js";

import { environmentService } from "../../../entities/project/api/environment.service";
import { projectKeys } from "../../../entities/project/queries/keys";
import { ProjectEnvironments } from "../../../features/project-environments/ProjectEnvironments";
import { useEscapeKey } from "../../../shared/hooks/useEscapeKey";
import { MSG } from "../../../shared/lib/messages";
import { ConfirmDialog } from "../../../shared/ui/confirm-dialog";
import { useToast } from "../../../shared/ui/toast";
import type { CreateEnvironmentRequest } from "../../../types";
import { ProjectSectionLayout } from "../components/ProjectSectionLayout";
import { useProjectContext } from "../hooks/useProjectContext";

export default function EnvironmentsSection() {
  const params = useParams<{ slug: string }>();
  const queryClient = useQueryClient();
  const { addToast } = useToast();
  const [showEnvForm, setShowEnvForm] = createSignal(false);
  const [hasAutoOpenedEnvForm, setHasAutoOpenedEnvForm] = createSignal(false);
  const [confirmDeleteEnv, setConfirmDeleteEnv] = createSignal<string | null>(null);

  const {
    projectsQuery,
    project,
    projectId,
    activeEnvName,
    setProjectActiveEnvName,
    environmentsQuery,
    canManageProject
  } = useProjectContext();

  createEffect(() => {
    const shouldAutoOpen =
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

  useEscapeKey(() => {
    setShowEnvForm(false);
  });

  const createEnvMutation = useMutation(() => ({
    mutationFn: (req: CreateEnvironmentRequest) => environmentService.create(req),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.environments(params.slug) });
      setShowEnvForm(false);
      addToast(MSG.ENV_CREATED, "success");
    },
    onError: () => addToast(MSG.ENV_CREATE_FAILED, "error")
  }));

  const deleteEnvMutation = useMutation(() => ({
    mutationFn: (environmentName: string) =>
      environmentService.delete(projectId(), environmentName),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.environments(params.slug) });
      addToast(MSG.ENV_DELETED, "success");
    },
    onError: () => addToast(MSG.ENV_DELETE_FAILED, "error")
  }));

  return (
    <>
      <ProjectSectionLayout
        section="environments"
        project={project()}
        projectLoading={projectsQuery.isLoading}
      >
        <ProjectEnvironments
          environments={environmentsQuery.status === "success" ? (environmentsQuery.data ?? []) : []}
          activeEnvName={activeEnvName()}
          setActiveEnvName={setProjectActiveEnvName}
          onCreateEnv={(name: string) => createEnvMutation.mutate({ projectId: projectId(), name })}
          onDeleteEnv={setConfirmDeleteEnv}
          showEnvForm={showEnvForm()}
          setShowEnvForm={setShowEnvForm}
          createEnvPending={createEnvMutation.isPending}
          canManage={canManageProject()}
        />
      </ProjectSectionLayout>

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
