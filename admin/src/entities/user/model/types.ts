// User domain types — source of truth for user-related contracts

export interface ProjectAccess {
  projectName: string;
  role: "editor" | "viewer";
}

export interface User {
  id: string;
  email: string;
  role: "admin" | "member";
  name: string;
  scope?: string;
  projects?: ProjectAccess[];
  passwordEnabled: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateUserRequest {
  name: string;
  email: string;
  role?: "admin" | "member";
  scope?: string;
}

export interface CreateUserResponse {
  user: User;
  invitationToken: string;
}

export interface GeneratePasswordResetResponse {
  passwordResetToken: string;
  expiresAt: string;
}

export interface DashboardCounts {
  projects: number;
  configEntries: number;
  users: number;
}
