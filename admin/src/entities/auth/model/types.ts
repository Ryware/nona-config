// Auth domain types — source of truth for auth-related contracts

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  username?: string;
  role: string;
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

export interface RegisterResult {
  success: boolean;
  response: LoginResponse | null;
  error: string | null;
}

export interface RegisterRequest {
  email: string;
  password: string;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  token: string;
  newPassword: string;
}

export interface InvitationDetails {
  email: string;
  name: string;
}
