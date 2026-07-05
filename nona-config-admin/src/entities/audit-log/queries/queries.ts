import { auditLogService } from "../api/audit-log.service";
import { auditLogKeys } from "./keys";

export const auditLogQueries = {
  list: () => ({
    queryKey: auditLogKeys.list(),
    queryFn: () => auditLogService.getAll(),
  }),
};
