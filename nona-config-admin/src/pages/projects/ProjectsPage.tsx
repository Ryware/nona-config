import { Title } from "@solidjs/meta";
import { useNavigate } from "@solidjs/router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/solid-query";
import { createMemo, createSignal, Show } from "solid-js";
import { canManageProjects, canManageProjectsFor } from "../../entities/auth/model/permissions";
import { authStore } from "../../entities/auth/model/store";
import { projectService } from "../../entities/project/api/project.service";
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
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { addToast } = useToast();

  const [showCreate, setShowCreate] = createSignal(false);
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

  return (
    <>
      <Title>Projects | Nona Config Admin</Title>
      <div class="space-y-6">
        {/* Page header */}
        <div class="flex items-start justify-between gap-4 sm:items-center">
          <div class="space-y-1.5">
            <h2
              data-testid="projects-heading"
              class="font-headline text-on-surface text-[17px] font-bold tracking-tight"
            >
              Projects
            </h2>
            <ProjectsStats
              isSuccess={projectsQuery.isSuccess}
              projects={allProjects()}
              filteredCount={filteredProjects().length}
            />
          </div>
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

        {/* Error banner */}
        <Show when={projectsQuery.isError}>
          <QueryErrorBanner
            message="Failed to load projects."
            onRetry={() => projectsQuery.refetch()}
          />
        </Show>

        {/* Search bar */}
        <Show when={projectsQuery.isSuccess && allProjects().length > 0}>
          <div class="max-w-sm">
            <Input
              data-testid="projects-search-input"
              type="text"
              placeholder="Search projects…"
              value={search()}
              onInput={(e: InputEvent & { currentTarget: HTMLInputElement }) =>
                setSearch(e.currentTarget.value)
              }
              leftIcon="search"
            />
          </div>
        </Show>

        {/* Create form */}
        <Show when={allowProjectManagement() && showCreate()}>
          <ProjectCreateForm
            onCancel={() => setShowCreate(false)}
            onSubmit={data => createMutation.mutate(data)}
            isPending={createMutation.isPending}
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
