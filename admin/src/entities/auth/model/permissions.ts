import type { User } from "../../user/model/types";
import { USER_ROLES } from "./roles";
import { authStore } from "./store";

export function canManageUsers(): boolean {
  const session = authStore.getSession();
  const role = session?.role?.toLowerCase();
  return role === USER_ROLES.ADMIN || role === USER_ROLES.EDITOR;
}

export function canResetPasswords(): boolean {
  return authStore.getSession()?.role?.toLowerCase() === USER_ROLES.ADMIN;
}

export function canManageUsersFor(user: User | undefined): boolean {
  const role = user?.role?.toLowerCase();
  return role === USER_ROLES.ADMIN || role === USER_ROLES.EDITOR;
}

export function canManageProjects(): boolean {
  const session = authStore.getSession();
  return session?.role?.toLowerCase() === USER_ROLES.ADMIN;
}

export function canManageProjectsFor(user: User | undefined): boolean {
  return user?.role?.toLowerCase() === USER_ROLES.ADMIN;
}

export function canManageProjectResources(projectName: string, users: User[]): boolean {
  const session = authStore.getSession();

  const currentUser = users.find(
    user => user.email.toLowerCase() === (session?.email ?? "").toLowerCase()
  );
  if (currentUser) {
    const role = currentUser.role?.toLowerCase();
    if (role === USER_ROLES.ADMIN || role === USER_ROLES.EDITOR) {
      return true;
    }

    return (
      currentUser.projects?.some(
        project =>
          project.projectName.toLowerCase() === projectName.toLowerCase() &&
          project.role.toLowerCase() === USER_ROLES.EDITOR
      ) ?? false
    );
  }

  const role = session?.role?.toLowerCase();
  if (role === USER_ROLES.ADMIN || role === USER_ROLES.EDITOR) {
    return true;
  }

  return false;
}

export function isCurrentUser(email: string): boolean {
  return (authStore.getSession()?.email ?? "").toLowerCase() === email.toLowerCase();
}
