export const USER_ROLES = {
  ADMIN: "admin",
  MEMBER: "member"
} as const;

export type UserRole = (typeof USER_ROLES)[keyof typeof USER_ROLES];
