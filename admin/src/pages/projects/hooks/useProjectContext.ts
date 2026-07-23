import { useParams } from "@solidjs/router";
import { useQuery } from "@tanstack/solid-query";
import { createEffect, createMemo } from "solid-js";

import { canManageProjectResources } from "../../../entities/auth/model/permissions";
import { environmentService } from "../../../entities/project/api/environment.service";
import { projectService } from "../../../entities/project/api/project.service";
import {
  getActiveEnvironmentName,
  setActiveEnvironmentName,
  syncActiveEnvironment
} from "../../../entities/project/model/active-environment";
import { setActiveProjectSlug } from "../../../entities/project/model/active-project";
import { projectKeys } from "../../../entities/project/queries/keys";
import { userService } from "../../../entities/user/api/user.service";
import { userKeys } from "../../../entities/user/queries/keys";
import type { Project } from "../../../types";


export function useProjectContext() {
  const params = useParams<{ slug: string }>();

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

  const activeEnvironment = createMemo(() => {
    const envs = environmentsQuery.status === "success" ? (environmentsQuery.data ?? []) : [];
    return envs.find(env => env.name === activeEnvName());
  });

  const activeEnvironmentKey = createMemo(() =>
    project() && activeEnvName() ? `${project()!.urlSlug}:${activeEnvName()}` : ""
  );

  const usersQuery = useQuery(() => ({
    queryKey: userKeys.list(),
    queryFn: () => userService.getAll(),
    enabled: !!project()
  }));

  const canManageProject = createMemo(() =>
    canManageProjectResources(
      projectId(),
      usersQuery.status === "success" ? (usersQuery.data ?? []) : []
    )
  );

  return {
    projectsQuery,
    project,
    projectId,
    activeEnvName,
    setProjectActiveEnvName,
    environmentsQuery,
    activeEnvironment,
    activeEnvironmentKey,
    usersQuery,
    canManageProject
  };
}
