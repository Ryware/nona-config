import { Title } from "@solidjs/meta";
import { useLocation, useNavigate } from "@solidjs/router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/solid-query";
import { createEffect, createMemo, createSignal, Show } from "solid-js";
import { canManageProjects, canManageProjectsFor } from "../../entities/auth/model/permissions";
import { authStore } from "../../entities/auth/model/store";
import { projectService } from "../../entities/project/api/project.service";
import { syncActiveProject } from "../../entities/project/model/active-project";
import { projectKeys } from "../../entities/project/queries/keys";
import { userService } from "../../entities/user/api/user.service";
import { userKeys } from "../../entities/user/queries/keys";
import { MSG } from "../../shared/lib/messages";
import { ConfirmDialog } from "../../shared/ui/confirm-dialog";
import { MIcon } from "../../shared/ui/icons";
import { Input } from "../../shared/ui/input";
import { QueryErrorBanner } from "../../shared/ui/QueryGuard";
import { useToast } from "../../shared/ui/toast";
import type { Project } from "../../types";
import { ProjectCreateForm } from "./components/ProjectCreateForm";
import { ProjectGrid } from "./components/ProjectGrid";
import { ProjectsStats } from "./components/ProjectsStats";

export default function ProjectsPage() {
  const location = useLocation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { addToast } = useToast();

  const [showCreate, setShowCreate] = createSignal(false);
  const [hasAutoOpenedCreate, setHasAutoOpenedCreate] = createSignal(false);
  const [deleteTarget, setDeleteTarget] = createSignal<Project | null>(null);
  const [search, setSearch] = createSignal("");
  const sessionAllowsProjectManagement = canManageProjects();

  const projectsQuery = useQuery(() => ({
    queryKey: projectKeys.list(),
    queryFn: () => projectService.getAll()
  }));

  const usersQuery = useQuery(() => ({
    queryKey: userKeys.list(),
    queryFn: () => userService.getAll()
  }));

  const createMutation = useMutation(() => ({
    mutationFn: (data: { name: string; description?: string }) => projectService.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.list() });
      setShowCreate(false);
      addToast(MSG.PROJECT_CREATED, "success");
    },
    onError: () => addToast(MSG.PROJECT_CREATE_FAILED, "error")
  }));

  const deleteMutation = useMutation(() => ({
    mutationFn: (projectName: string) => projectService.delete(projectName),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.list() });
      setDeleteTarget(null);
      addToast(MSG.PROJECT_DELETED, "success");
    },
    onError: () => addToast(MSG.PROJECT_DELETE_FAILED, "error")
  }));

  const allProjects = () => (projectsQuery.status === "success" ? (projectsQuery.data ?? []) : []);
  const currentUser = createMemo(() => {
    const email = authStore.getSession()?.email?.toLowerCase() ?? "";
    const users = usersQuery.status === "success" ? (usersQuery.data ?? []) : [];
    return users.find(user => user.email.toLowerCase() === email);
  });
  const allowProjectManagement = createMemo(
    () =>
      usersQuery.status === "success"
        ? canManageProjectsFor(currentUser())
        : sessionAllowsProjectManagement
  );
  const filteredProjects = createMemo(() => {
    const q = search().toLowerCase().trim();
    if (!q) return allProjects();
    return allProjects().filter(
      (p: Project) =>
        p.name.toLowerCase().includes(q) ||
        p.urlSlug.toLowerCase().includes(q) ||
        (p.description ?? "").toLowerCase().includes(q)
    );
  });

  createEffect(() => {
    if (projectsQuery.status === "success") {
      syncActiveProject(allProjects());
    }
  });

  createEffect(() => {
    const shouldAutoOpen =
      projectsQuery.status === "success" &&
      allowProjectManagement() &&
      allProjects().length === 0;

    if (shouldAutoOpen && !hasAutoOpenedCreate()) {
      setShowCreate(true);
      setHasAutoOpenedCreate(true);
      return;
    }

    if (!shouldAutoOpen && hasAutoOpenedCreate()) {
      setHasAutoOpenedCreate(false);
    }
  });

  createEffect(() => {
    if (new URLSearchParams(location.search).get("new") === "1") {
      setShowCreate(allowProjectManagement());
      navigate("/projects", { replace: true });
    }
  });

  return (
    <>
      <Title>Projects | Nona Config Admin</Title>
      <div class="space-y-6">
        <section class="bg-surface-container-low border-outline-variant/15 space-y-4 rounded-2xl border p-5">
          {/* Section header */}
          <div class="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
            <div>
              <h2
                data-testid="projects-heading"
                class="text-outline font-headline flex items-center gap-1.5 text-[10px] font-bold tracking-widest uppercase"
              >
                <MIcon name="folder" class="text-[15px]" />
                Projects
              </h2>
              <div class="mt-1">
                <ProjectsStats
                  isSuccess={projectsQuery.isSuccess}
                  projects={allProjects()}
                  filteredCount={filteredProjects().length}
                />
              </div>
            </div>

            <div class="flex flex-col gap-2 md:w-auto md:flex-row md:flex-wrap md:items-center md:justify-end">
              <Show when={projectsQuery.isSuccess && allProjects().length > 0}>
                <Input
                  data-testid="projects-search-input"
                  type="text"
                  placeholder="Search projects…"
                  value={search()}
                  onInput={(e: InputEvent & { currentTarget: HTMLInputElement }) =>
                    setSearch(e.currentTarget.value)
                  }
                  class="h-10 w-full md:w-64"
                  leftIcon="search"
                  wrapperStyle="w-full md:w-auto"
                />
              </Show>
              <Show when={allowProjectManagement()}>
                <button
                  data-testid="projects-new-button"
                  onClick={() => setShowCreate(!showCreate())}
                  class="bg-primary text-on-primary flex shrink-0 cursor-pointer items-center gap-2 rounded-lg border-0 px-4 py-2 text-[13px] font-semibold transition-all hover:brightness-105 active:scale-[0.98]"
                >
                  <MIcon name={showCreate() ? "close" : "add"} class="text-[17px]" />
                  {showCreate() ? "Cancel" : "New Project"}
                </button>
              </Show>
            </div>
          </div>

          {/* Error banner */}
          <Show when={projectsQuery.isError}>
            <QueryErrorBanner
              message="Failed to load projects."
              onRetry={() => projectsQuery.refetch()}
            />
          </Show>

          {/* Create form */}
          <Show when={allowProjectManagement() && showCreate()}>
            <ProjectCreateForm
              onCancel={() => setShowCreate(false)}
              onSubmit={data => createMutation.mutate(data)}
              isPending={createMutation.isPending}
              projects={allProjects()}
            />
          </Show>

          {/* Project grid */}
          <ProjectGrid
            isLoading={projectsQuery.isLoading}
            isSuccess={projectsQuery.isSuccess}
            projects={allProjects()}
            filteredProjects={filteredProjects()}
            search={search()}
            onNavigate={slug => navigate(`/projects/${slug}`)}
            onDeleteTarget={setDeleteTarget}
            onCreateClick={() => setShowCreate(true)}
            canCreateProjects={allowProjectManagement()}
            canDeleteProjects={allowProjectManagement()}
          />
        </section>

        {/* Delete confirmation modal */}
        <ConfirmDialog
          open={deleteTarget() !== null}
          title="Delete Project?"
          message={
            <span>
              This will permanently delete{" "}
              <span class="text-primary font-mono font-bold">{deleteTarget()?.name}</span> and all
              its configuration data.
            </span>
          }
          confirmLabel="Delete"
          isLoading={deleteMutation.isPending}
          onConfirm={() => deleteMutation.mutate(deleteTarget()!.name)}
          onCancel={() => setDeleteTarget(null)}
          variant="danger"
          testId="delete-project-dialog"
          confirmTestId="delete-project-confirm-button"
          cancelTestId="delete-project-cancel-button"
        />
      </div>
    </>
  );
}
