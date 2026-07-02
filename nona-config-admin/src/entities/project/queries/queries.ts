/**
 * Query factory functions for the project entity.
 * Import these into components instead of writing raw queryKey/queryFn.
 */
import { projectService } from "../api/project.service";
import { environmentService } from "../api/environment.service";
import { configEntryService } from "../api/config-entry.service";
import { projectKeys } from "./keys";

export const projectQueries = {
  list: () => ({
    queryKey: projectKeys.list(),
    queryFn: () => projectService.getAll(),
  }),

  detail: (slug: string) => ({
    queryKey: projectKeys.detail(slug),
    queryFn: () => projectService.getById(slug),
    enabled: !!slug,
  }),

  environments: (projectName: string, slug: string) => ({
    queryKey: projectKeys.environments(slug),
    queryFn: () => environmentService.getAll(projectName),
    enabled: !!projectName,
  }),

  configEntries: (projectName: string, slug: string, env: string) => ({
    queryKey: projectKeys.configEntries(slug, env),
    queryFn: () => configEntryService.getAll(projectName, env),
    enabled: !!projectName && !!env,
  }),

  apiKeys: (projectName: string, slug: string) => ({
    queryKey: projectKeys.apiKeys(slug),
    queryFn: () => projectService.listApiKeys(projectName),
    enabled: !!projectName,
  }),
};
