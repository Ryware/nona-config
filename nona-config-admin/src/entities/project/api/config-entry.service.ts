import { apiClient } from '../../../shared/api/client';
import type {
  ConfigEntry,
  ConfigEntryVersion,
  CreatedParameterShareLink,
  CreateParameterShareLinkRequest,
  ParameterShareLink,
  SharedParameter,
  RollbackConfigEntryRequest,
  UpdateSharedParameterRequest,
  UpdateConfigEntryRequest
} from '../../../types';

const pathSegment = (value: string | undefined) => encodeURIComponent(value ?? "");

export const configEntryService = {
  async getAll(projectId?: string, environmentName?: string): Promise<ConfigEntry[]> {
    return apiClient.get<ConfigEntry[]>(`/admin/projects/${pathSegment(projectId)}/environments/${pathSegment(environmentName)}/config-entries`);
  },

  async getById(projectId: string, environmentName: string, id: string): Promise<ConfigEntry> {
    return apiClient.get<ConfigEntry>(`/admin/projects/${pathSegment(projectId)}/environments/${pathSegment(environmentName)}/config-entries/${pathSegment(id)}`);
  },

  async upsert(projectId: string, environmentName: string, id: string, data: UpdateConfigEntryRequest): Promise<ConfigEntry> {
    return apiClient.put<ConfigEntry>(`/admin/projects/${pathSegment(projectId)}/environments/${pathSegment(environmentName)}/config-entries/${pathSegment(id)}`, data);
  },

  async history(projectId: string, environmentName: string, id: string): Promise<ConfigEntryVersion[]> {
    return apiClient.get<ConfigEntryVersion[]>(`/admin/projects/${pathSegment(projectId)}/environments/${pathSegment(environmentName)}/config-entries/${pathSegment(id)}/history`);
  },

  async rollback(projectId: string, environmentName: string, id: string, data: RollbackConfigEntryRequest): Promise<ConfigEntry> {
    return apiClient.post<ConfigEntry>(`/admin/projects/${pathSegment(projectId)}/environments/${pathSegment(environmentName)}/config-entries/${pathSegment(id)}/rollback`, data);
  },

  async listShareLinks(projectId: string, environmentName: string, id: string): Promise<ParameterShareLink[]> {
    return apiClient.get<ParameterShareLink[]>(`/admin/projects/${pathSegment(projectId)}/environments/${pathSegment(environmentName)}/config-entries/${pathSegment(id)}/share-links`);
  },

  async createShareLink(projectId: string, environmentName: string, id: string, data: CreateParameterShareLinkRequest): Promise<CreatedParameterShareLink> {
    return apiClient.post<CreatedParameterShareLink>(`/admin/projects/${pathSegment(projectId)}/environments/${pathSegment(environmentName)}/config-entries/${pathSegment(id)}/share-links`, data);
  },

  async revokeShareLink(projectId: string, environmentName: string, id: string, shareLinkId: number): Promise<void> {
    return apiClient.delete<void>(`/admin/projects/${pathSegment(projectId)}/environments/${pathSegment(environmentName)}/config-entries/${pathSegment(id)}/share-links/${shareLinkId}`);
  },

  async delete(projectId: string, environmentName: string, id: string): Promise<void> {
    return apiClient.delete<void>(`/admin/projects/${pathSegment(projectId)}/environments/${pathSegment(environmentName)}/config-entries/${pathSegment(id)}`);
  },
};

export const sharedParameterService = {
  async get(token: string): Promise<SharedParameter> {
    return apiClient.get<SharedParameter>(`/public/share-links/${pathSegment(token)}`);
  },

  async update(token: string, data: UpdateSharedParameterRequest): Promise<SharedParameter> {
    return apiClient.put<SharedParameter>(`/public/share-links/${pathSegment(token)}`, data);
  },
};
