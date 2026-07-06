// User domain types — source of truth for user-related contracts

export interface ProjectAccess {
  projectName: string;
  role: string;
}

export interface User {
  id: string;
  email: string;
  isAdmin: boolean;
  role: string;
  name: string;
  scope?: string;
  projects?: ProjectAccess[];
  createdAt: string;
  updatedAt?: string;
}

export interface CreateUserRequest {
  name: string;
  email: string;
  role?: string;
  scope?: string;
}

export interface CreateUserResponse {
  user: User;
  invitationToken: string;
}

export interface DashboardCounts {
  projects: number;
  configEntries: number;
  users: number;
}
