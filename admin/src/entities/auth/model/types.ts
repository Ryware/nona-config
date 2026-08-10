// Auth domain types — source of truth for auth-related contracts

export type OrganizationRole = "admin" | "member";

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  username?: string;
  role: OrganizationRole;
  expiresAt: string;
}

export interface SsoProviderConfig {
  enabled: boolean;
  clientId: string | null;
  authority?: string | null;
  tenantId?: string | null;
}

export interface SsoConfig {
  google: SsoProviderConfig;
  microsoft: SsoProviderConfig;
}

export interface RegisterRequest {
  email: string;
  password: string;
}

export interface InvitationDetails {
  email: string;
  name: string;
}
