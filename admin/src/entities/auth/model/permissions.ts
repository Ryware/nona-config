import { USER_ROLES } from "./roles";
import { authStore } from "./store";

export function canManageUsers(): boolean {
  return authStore.getSession()?.role === USER_ROLES.ADMIN;
}

export function canResetPasswords(): boolean {
  return authStore.getSession()?.role === USER_ROLES.ADMIN;
}

export function canManageProjects(): boolean {
  return authStore.getSession()?.role === USER_ROLES.ADMIN;
}
