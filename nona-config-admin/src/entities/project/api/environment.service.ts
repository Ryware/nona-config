import { apiClient } from '../../../shared/api/client';
import type { Environment, CreateEnvironmentRequest } from '../../../types';

export const environmentService = {
  async getAll(projectId?: string): Promise<Environment[]> {
    return apiClient.get<Environment[]>(`/admin/projects/${projectId}/environments`);
  },

  async getById(projectId: string, environmentName: string): Promise<Environment> {
    return apiClient.get<Environment>(`/admin/projects/${projectId}/environments/${environmentName}`);
  },

  async create(data: CreateEnvironmentRequest): Promise<Environment> {
    return apiClient.post<Environment>(`/admin/projects/${data.projectId}/environments`, data);
  },

  async delete(projectId: string, environmentName: string): Promise<void> {
    return apiClient.delete(`/admin/projects/${projectId}/environments/${environmentName}`);
  },
};
