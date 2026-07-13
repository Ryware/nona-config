import { authStore } from "../model/store";

/**
 * Returns the current user's email from the persisted session.
 * Falls back to empty string if not authenticated.
 *
 * @deprecated Direct JWT parsing has been removed. Email is now derived
 * from auth_session metadata saved at login time (see authStore.saveSession).
 */
export function getCurrentUserEmail(): string {
  return authStore.getSession()?.email ?? "";
}
