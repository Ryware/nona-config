export const USER_ROLES = {
  ADMIN: "admin",
  EDITOR: "editor",
  VIEWER: "viewer"
} as const;

export type UserRole = (typeof USER_ROLES)[keyof typeof USER_ROLES];
