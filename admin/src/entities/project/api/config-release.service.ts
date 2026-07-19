import { apiClient } from "../../../shared/api/client";
import type {
  ConfigEntry,
  ConfigRelease,
  ConfigReleaseDetails,
  Environment,
  PublishConfigReleaseRequest,
  SetActiveConfigReleaseRequest,
} from "../../../types";

const segment = (value: string) => encodeURIComponent(value);

export const configReleaseService = {
  async getAll(projectId: string, environmentName: string): Promise<ConfigRelease[]> {
    return apiClient.get<ConfigRelease[]>(
      `/admin/projects/${segment(projectId)}/environments/${segment(environmentName)}/releases`,
    );
  },

  async get(
    projectId: string,
    environmentName: string,
    version: string,
  ): Promise<ConfigReleaseDetails> {
    return apiClient.get<ConfigReleaseDetails>(
      `/admin/projects/${segment(projectId)}/environments/${segment(environmentName)}/releases/${segment(version)}`,
    );
  },

  async publish(
    projectId: string,
    environmentName: string,
    data: PublishConfigReleaseRequest,
  ): Promise<ConfigReleaseDetails> {
    return apiClient.post<ConfigReleaseDetails>(
      `/admin/projects/${segment(projectId)}/environments/${segment(environmentName)}/releases`,
      data,
    );
  },

  async setActive(
    projectId: string,
    environmentName: string,
    data: SetActiveConfigReleaseRequest,
  ): Promise<Environment> {
    return apiClient.put<Environment>(
      `/admin/projects/${segment(projectId)}/environments/${segment(environmentName)}/active-release`,
      data,
    );
  },

  async clearActive(projectId: string, environmentName: string): Promise<Environment> {
    return apiClient.delete<Environment>(
      `/admin/projects/${segment(projectId)}/environments/${segment(environmentName)}/active-release`,
    );
  },

  async createDraft(
    projectId: string,
    environmentName: string,
    version: string,
  ): Promise<ConfigEntry[]> {
    return apiClient.post<ConfigEntry[]>(
      `/admin/projects/${segment(projectId)}/environments/${segment(environmentName)}/releases/${segment(version)}/draft`,
    );
  },

  async delete(projectId: string, environmentName: string, version: string): Promise<void> {
    await apiClient.delete(
      `/admin/projects/${segment(projectId)}/environments/${segment(environmentName)}/releases/${segment(version)}`,
    );
  },
};
