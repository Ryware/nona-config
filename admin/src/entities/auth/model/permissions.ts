import type { User } from "../../user/model/types";
import { authStore } from "./store";

export function canManageUsers(): boolean {
  const session = authStore.getSession();
  const role = session?.role?.toLowerCase();
  return session?.isAdmin === true || role === "admin" || role === "editor";
}

export function canManageUsersFor(user: User | undefined): boolean {
  const role = user?.role?.toLowerCase();
  return user?.isAdmin === true || role === "admin" || role === "editor";
}

export function canManageProjects(): boolean {
  const session = authStore.getSession();
  return session?.isAdmin === true || session?.role?.toLowerCase() === "admin";
}

export function canManageProjectsFor(user: User | undefined): boolean {
  return user?.isAdmin === true || user?.role?.toLowerCase() === "admin";
}

export function canManageProjectResources(projectName: string, users: User[]): boolean {
  const session = authStore.getSession();

  const currentUser = users.find(
    user => user.email.toLowerCase() === (session?.email ?? "").toLowerCase()
  );
  if (currentUser) {
    const role = currentUser.role?.toLowerCase();
    if (currentUser.isAdmin || role === "admin" || role === "editor") {
      return true;
    }

    return (
      currentUser.projects?.some(
        project =>
          project.projectName.toLowerCase() === projectName.toLowerCase() &&
          project.role.toLowerCase() === "editor"
      ) ?? false
    );
  }

  const role = session?.role?.toLowerCase();
  if (session?.isAdmin || role === "admin" || role === "editor") {
    return true;
  }

  return false;
}

export function isCurrentUser(email: string): boolean {
  return (authStore.getSession()?.email ?? "").toLowerCase() === email.toLowerCase();
}
