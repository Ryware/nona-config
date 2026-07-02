/**
 * Mutation factory functions for the project entity.
 * Each factory returns a MutationOptions object — callers add their own onSuccess callbacks.
 */
import { projectService } from "../api/project.service";
import { environmentService } from "../api/environment.service";
import { configEntryService } from "../api/config-entry.service";
import type {
  CreateProjectRequest,
  UpdateProjectRequest,
  CreateEnvironmentRequest,
  CreateConfigEntryRequest,
  UpdateConfigEntryRequest,
  CreateApiKeyRequest,
} from "../model/types";

export const projectMutations = {
  createProject: () => ({
    mutationFn: (data: CreateProjectRequest) => projectService.create(data),
  }),

  updateProject: (slug: string) => ({
    mutationFn: (data: UpdateProjectRequest) =>
      projectService.update(slug, data),
  }),

  deleteProject: () => ({
    mutationFn: (slug: string) => projectService.delete(slug),
  }),

  createApiKey: () => ({
    mutationFn: ({
      projectId,
      data,
    }: {
      projectId: string;
      data: CreateApiKeyRequest;
    }) => projectService.createApiKey(projectId, data),
  }),

  deleteApiKey: () => ({
    mutationFn: ({
      projectId,
      apiKeyId,
    }: {
      projectId: string;
      apiKeyId: string;
    }) => projectService.deleteApiKey(projectId, apiKeyId),
  }),

  createEnvironment: () => ({
    mutationFn: (data: CreateEnvironmentRequest) =>
      environmentService.create(data),
  }),

  deleteEnvironment: () => ({
    mutationFn: ({
      projectId,
      name,
    }: {
      projectId: string;
      name: string;
    }) => environmentService.delete(projectId, name),
  }),

  upsertConfigEntry: () => ({
    mutationFn: ({
      projectId,
      env,
      key,
      data,
    }: {
      projectId: string;
      env: string;
      key: string;
      data: CreateConfigEntryRequest | UpdateConfigEntryRequest;
    }) => configEntryService.upsert(projectId, env, key, data),
  }),

  deleteConfigEntry: () => ({
    mutationFn: ({
      projectId,
      env,
      key,
    }: {
      projectId: string;
      env: string;
      key: string;
    }) => configEntryService.delete(projectId, env, key),
  }),
};
