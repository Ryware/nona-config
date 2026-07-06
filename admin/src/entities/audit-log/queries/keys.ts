export const auditLogKeys = {
  all: () => ["audit-log"] as const,
  list: () => [...auditLogKeys.all(), "list"] as const,
} as const;
