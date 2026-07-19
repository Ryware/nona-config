import { useLocation, useNavigate } from "@solidjs/router";
import { useQuery } from "@tanstack/solid-query";
import { createEffect, createMemo } from "solid-js";
import { canManageProjects } from "../../entities/auth/model/permissions";
import { environmentService } from "../../entities/project/api/environment.service";
import { projectService } from "../../entities/project/api/project.service";
import {
  getActiveEnvironmentName,
  setActiveEnvironmentName,
  syncActiveEnvironment,
} from "../../entities/project/model/active-environment";
import {
  getActiveProjectSlug,
  setActiveProjectSlug,
  syncActiveProject,
} from "../../entities/project/model/active-project";
import { projectKeys } from "../../entities/project/queries/keys";
import { Select } from "../../shared/ui/select";
import { ThemeToggle } from "../../shared/ui/ThemeToggle";
import { Breadcrumbs } from "./Breadcrumbs";

interface HeaderProps {
  onMenuToggle: () => void;
  isSidebarOpen: boolean;
}

const MANAGE_PROJECTS_OPTION = "__manage_projects__";
const MANAGE_ENVIRONMENTS_OPTION = "__manage_environments__";

export function getProjectNavigationPath(pathname: string, slug: string) {
  if (pathname.endsWith("/environments")) {
    return `/projects/${slug}/environments`;
  }

  if (pathname.endsWith("/shared-links")) {
    return `/projects/${slug}/shared-links`;
  }

  if (pathname.endsWith("/api-keys")) {
    return `/projects/${slug}/api-keys`;
  }

  if (pathname.endsWith("/releases")) {
    return `/projects/${slug}/releases`;
  }

  return `/projects/${slug}`;
}

export function Header(props: HeaderProps) {
  const location = useLocation();
  const navigate = useNavigate();
  const canCreateProjects = canManageProjects();
  const projectsQuery = useQuery(() => ({
    queryKey: projectKeys.list(),
    queryFn: () => projectService.getAll(),
  }));

  const projects = () => (projectsQuery.status === "success" ? (projectsQuery.data ?? []) : []);
  const activeProject = createMemo(() =>
    projects().find(project => project.urlSlug === getActiveProjectSlug())
  );

  const environmentsQuery = useQuery(() => ({
    queryKey: projectKeys.environments(activeProject()?.urlSlug ?? ""),
    queryFn: () => environmentService.getAll(activeProject()!.name),
    enabled: !!activeProject(),
  }));

  const environments = () =>
    environmentsQuery.status === "success" ? (environmentsQuery.data ?? []) : [];

  createEffect(() => {
    if (projectsQuery.status === "success") {
      syncActiveProject(projects());
    }
  });

  createEffect(() => {
    const project = activeProject();
    if (!project || environmentsQuery.status !== "success") {
      return;
    }

    syncActiveEnvironment(project.urlSlug, environments());
  });

  const projectOptions = createMemo(() => [
    ...projects().map(project => ({
      value: project.urlSlug,
      label: project.name,
    })),
    ...(canCreateProjects
      ? [{ value: MANAGE_PROJECTS_OPTION, label: "List or Create Projects" }]
      : []),
  ]);

  const environmentOptions = createMemo(() => [
    ...environments().map(environment => ({
      value: environment.name,
      label: environment.name,
    })),
    ...(activeProject()
      ? [
          {
            value: MANAGE_ENVIRONMENTS_OPTION,
            label: "List or Create Environments",
          },
        ]
      : []),
  ]);

  const handleProjectChange = (slug: string) => {
    if (slug === MANAGE_PROJECTS_OPTION) {
      navigate("/projects");
      return;
    }

    setActiveProjectSlug(slug);
    navigate(getProjectNavigationPath(location.pathname, slug));
  };

  const handleEnvironmentChange = (environmentName: string) => {
    const project = activeProject();
    if (!project) {
      return;
    }

    if (environmentName === MANAGE_ENVIRONMENTS_OPTION) {
      navigate(`/projects/${project.urlSlug}/environments`);
      return;
    }

    setActiveEnvironmentName(project.urlSlug, environmentName);
  };

  return (
    <header class="sticky top-0 z-40 w-full shrink-0 border-b border-outline-variant/15 bg-background/80 backdrop-blur-md">
      <div class="flex min-h-14 items-center gap-3 px-5 md:px-7">
        <button
          onClick={() => props.onMenuToggle()}
          class="lg:hidden -ml-1 flex items-center justify-center rounded-lg border-0 bg-transparent p-2 text-on-surface-variant cursor-pointer hover:text-on-surface focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
          aria-label="Toggle navigation menu"
        >
          <span class="material-symbols-outlined text-2xl">
            {props.isSidebarOpen ? "close" : "menu"}
          </span>
        </button>

        <Breadcrumbs />

        <div class="hidden min-w-0 flex-1 lg:flex lg:items-center lg:justify-end lg:gap-5">
          <div class="flex min-w-0 max-w-[36rem] flex-[1.15] items-center gap-3">
            <span class="shrink-0 text-[11px] font-semibold uppercase tracking-[0.08em] text-on-surface-variant">
              Active Project
            </span>
            <Select
              value={getActiveProjectSlug()}
              onChange={handleProjectChange}
              options={projectOptions()}
              placeholder={projectsQuery.isLoading ? "Loading projects..." : "Select a project"}
              disabled={projectsQuery.isLoading || (projects().length === 0 && !canCreateProjects)}
              class="h-9 w-full min-w-0 rounded-xl border-outline-variant/20 bg-surface-container-low text-[12px]"
            />
          </div>

          <div class="flex min-w-0 max-w-[26rem] flex-1 items-center gap-3">
            <span class="shrink-0 text-[11px] font-semibold uppercase tracking-[0.08em] text-on-surface-variant">
              Active Environment
            </span>
            <Select
              value={activeProject() ? getActiveEnvironmentName(activeProject()!.urlSlug) : ""}
              onChange={handleEnvironmentChange}
              options={environmentOptions()}
              placeholder={
                activeProject()
                  ? environmentsQuery.isLoading
                    ? "Loading environments..."
                    : "Select an environment"
                  : "Select a project first"
              }
              disabled={!activeProject() || environmentsQuery.isLoading}
              class="h-9 w-full min-w-0 rounded-xl border-outline-variant/20 bg-surface-container-low text-[12px]"
            />
          </div>
        </div>

        <div class="flex shrink-0 items-center gap-2">
          <ThemeToggle />

          <div class="h-5 w-px bg-outline-variant/20" />

          <a
            class="flex items-center gap-1 text-[11px] font-medium text-outline transition-colors hover:text-primary"
            href="https://www.nonaconfig.com/support"
            target="_blank"
            rel="noopener noreferrer"
          >
            <span class="material-symbols-outlined text-[15px]">contact_support</span>
            <span class="hidden md:inline">Support</span>
          </a>
          <a
            class="flex items-center gap-1 text-[11px] font-medium text-outline transition-colors hover:text-primary"
            href="https://www.nonaconfig.com/docs"
            target="_blank"
            rel="noopener noreferrer"
          >
            <span class="material-symbols-outlined text-[15px]">terminal</span>
            <span class="hidden md:inline">API Docs</span>
          </a>
        </div>
      </div>

      <div class="space-y-3 border-t border-outline-variant/10 px-5 py-3 md:px-7 lg:hidden">
        <div class="grid gap-3 sm:grid-cols-2">
          <div class="space-y-1.5">
            <span class="block text-[11px] font-semibold uppercase tracking-[0.08em] text-on-surface-variant">
              Active Project
            </span>
            <Select
              value={getActiveProjectSlug()}
              onChange={handleProjectChange}
              options={projectOptions()}
              placeholder={projectsQuery.isLoading ? "Loading projects..." : "Select a project"}
              disabled={projectsQuery.isLoading || (projects().length === 0 && !canCreateProjects)}
              class="h-10 w-full rounded-xl border-outline-variant/20 bg-surface-container-low text-[12px]"
            />
          </div>

          <div class="space-y-1.5">
            <span class="block text-[11px] font-semibold uppercase tracking-[0.08em] text-on-surface-variant">
              Active Environment
            </span>
            <Select
              value={activeProject() ? getActiveEnvironmentName(activeProject()!.urlSlug) : ""}
              onChange={handleEnvironmentChange}
              options={environmentOptions()}
              placeholder={
                activeProject()
                  ? environmentsQuery.isLoading
                    ? "Loading environments..."
                    : "Select an environment"
                  : "Select a project first"
              }
              disabled={!activeProject() || environmentsQuery.isLoading}
              class="h-10 w-full rounded-xl border-outline-variant/20 bg-surface-container-low text-[12px]"
            />
          </div>
        </div>
      </div>
    </header>
  );
}
