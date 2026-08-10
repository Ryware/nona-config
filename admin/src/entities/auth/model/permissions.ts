import { authStore } from "./store";

export function canManageUsers(): boolean {
  return authStore.getSession()?.role === "admin";
}

export function canManageProjects(): boolean {
  return authStore.getSession()?.role === "admin";
}
