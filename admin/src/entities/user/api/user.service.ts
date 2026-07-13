import { apiClient } from "../../../shared/api/client";
import type { User, CreateUserRequest, CreateUserResponse, ProjectAccess } from "../../../types";

export interface UpdateUserRequest {
  name?: string;
  email?: string;
  password?: string;
  role?: string;
}

export const userService = {
  async getAll(): Promise<User[]> {
    return apiClient.get<User[]>("/admin/users");
  },

  async getById(id: string): Promise<User> {
    return apiClient.get<User>(`/admin/users/${id}`);
  },

  async create(data: CreateUserRequest): Promise<CreateUserResponse> {
    return apiClient.post<CreateUserResponse>("/admin/users", data);
  },

  async update(id: string, data: UpdateUserRequest): Promise<User> {
    return apiClient.put<User>(`/admin/users/${id}`, data);
  },

  async delete(id: string): Promise<void> {
    return apiClient.delete(`/admin/users/${id}`);
  },

  async addProject(userId: string, projectName: string, role: string): Promise<ProjectAccess> {
    return apiClient.put<ProjectAccess>(
      `/admin/users/${userId}/projects/${projectName}`,
      { role },
    );
  },

  async removeProject(userId: string, projectName: string): Promise<void> {
    return apiClient.delete(`/admin/users/${userId}/projects/${projectName}`);
  },
};
