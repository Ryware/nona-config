import type { AuditLogQuery } from "../model/types";

export const auditLogKeys = {
  all: () => ["audit-log"] as const,
  list: (query: AuditLogQuery) => [...auditLogKeys.all(), "list", query] as const,
} as const;
