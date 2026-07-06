// Project domain types — source of truth for project-related contracts

export interface Project {
  id: string;
  urlSlug: string;
  name: string;
  description?: string;
  environments: string[];
  createdAt: string;
  updatedAt: string;
}

export interface CreateProjectRequest {
  name: string;
  description?: string;
}

export interface UpdateProjectRequest {
  name: string;
  description?: string;
}

export interface Environment {
  project: string;
  name: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateEnvironmentRequest {
  projectId: string;
  name: string;
}

export interface ConfigEntry {
  project: string;
  environment: string;
  key: string;
  value: string;
  contentType: 'text' | 'number' | 'boolean' | 'json';
  scope: 'client' | 'server' | 'all';
  activeVersion: number;
  createdAt: string;
  updatedAt: string;
}

export interface ConfigEntryVersion {
  project: string;
  environment: string;
  key: string;
  version: number;
  value: string;
  contentType: 'text' | 'number' | 'boolean' | 'json';
  scope: 'client' | 'server' | 'all';
  createdAt: string;
  actor: string;
}

export interface ParameterShareLink {
  id: number;
  project: string;
  environment: string;
  key: string;
  canEdit: boolean;
  createdBy: string;
  createdAt: string;
  expiresAt: string;
  revokedAt?: string | null;
}

export interface CreatedParameterShareLink extends ParameterShareLink {
  token: string;
}

export interface CreateParameterShareLinkRequest {
  expiration?: "1h" | "1d" | "3d" | "30d" | "12m";
  canEdit: boolean;
}

export interface SharedParameter {
  environment: string;
  key: string;
  value: string;
  contentType: 'text' | 'number' | 'boolean' | 'json';
  canEdit: boolean;
  expiresAt: string;
}

export interface UpdateSharedParameterRequest {
  value: string;
}

export interface CreateConfigEntryRequest {
  projectId: string;
  key: string;
  value: string;
  contentType: 'text' | 'number' | 'boolean' | 'json';
  scope: 'client' | 'server' | 'all';
}

export interface UpdateConfigEntryRequest {
  value: string;
  contentType?: 'text' | 'number' | 'boolean' | 'json';
  scope?: 'client' | 'server' | 'all';
}

export interface RollbackConfigEntryRequest {
  version: number;
}

export interface ApiKey {
  id: string;
  name: string;
  key: string;
  project: string;
  environment: string | null;
  scope: 'client' | 'server' | 'all';
  createdAt: string;
  updatedAt: string;
}

export interface CreateApiKeyRequest {
  name: string;
  environment?: string | null;
  scope?: 'client' | 'server' | 'all';
}
