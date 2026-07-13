import { apiClient } from "../../../shared/api/client";
import type {
  Project,
  CreateProjectRequest,
  UpdateProjectRequest,
  ApiKey,
  CreateApiKeyRequest,
} from "../../../types";

export const projectService = {
  async getAll(): Promise<Project[]> {
    return apiClient.get<Project[]>("/admin/projects");
  },

  async getById(slug: string): Promise<Project> {
    return apiClient.get<Project>(`/admin/projects/${slug}`);
  },

  async create(data: CreateProjectRequest): Promise<Project> {
    return apiClient.post<Project>("/admin/projects", data);
  },

  async update(slug: string, data: UpdateProjectRequest): Promise<Project> {
    return apiClient.put<Project>(`/admin/projects/${slug}`, data);
  },

  async delete(slug: string): Promise<void> {
    return apiClient.delete(`/admin/projects/${slug}`);
  },

  async listApiKeys(projectId: string): Promise<ApiKey[]> {
    return apiClient.get<ApiKey[]>(`/admin/projects/${projectId}/api-keys`);
  },

  async createApiKey(projectId: string, data: CreateApiKeyRequest): Promise<ApiKey> {
    return apiClient.post<ApiKey>(`/admin/projects/${projectId}/api-keys`, data);
  },

  async deleteApiKey(projectId: string, apiKeyId: string): Promise<void> {
    return apiClient.delete(`/admin/projects/${projectId}/api-keys/${apiKeyId}`);
  },
};
