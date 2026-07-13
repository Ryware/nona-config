/**
 * Backward-compatible re-exports from entity model types.
 * Prefer importing directly from entity models in new code:
 *   import type { Project } from "../entities/project/model/types"
 *   import type { User } from "../entities/user/model/types"
 */

export type {
  LoginRequest,
  LoginResponse,
  SsoProviderConfig,
  SsoConfig,
  RegisterResult,
  RegisterRequest,
  ForgotPasswordRequest,
  ResetPasswordRequest,
  InvitationDetails,
} from "../entities/auth/model/types";

export type {
  Project,
  CreateProjectRequest,
  UpdateProjectRequest,
  Environment,
  CreateEnvironmentRequest,
  ConfigEntry,
  ConfigEntryVersion,
  ParameterShareLink,
  CreatedParameterShareLink,
  CreateParameterShareLinkRequest,
  SharedParameter,
  UpdateSharedParameterRequest,
  CreateConfigEntryRequest,
  UpdateConfigEntryRequest,
  RollbackConfigEntryRequest,
  ApiKey,
  CreateApiKeyRequest,
} from "../entities/project/model/types";

export type {
  ProjectAccess,
  User,
  CreateUserRequest,
  CreateUserResponse,
  DashboardCounts,
} from "../entities/user/model/types";

export type { AuditLog } from "../entities/audit-log/model/types";

// Cross-domain shared types (not entity-specific)
export interface ApiError {
  error: string;
  message?: string;
  errorCode?: string;
}

export interface PaginatedResponse<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}
