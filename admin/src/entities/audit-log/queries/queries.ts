import { auditLogService } from "../api/audit-log.service";
import type { AuditLogQuery } from "../model/types";
import { auditLogKeys } from "./keys";

export const auditLogQueries = {
  list: (query: AuditLogQuery) => ({
    queryKey: auditLogKeys.list(query),
    queryFn: () => auditLogService.getPage(query),
  }),
};
