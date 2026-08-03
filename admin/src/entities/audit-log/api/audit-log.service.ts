import { apiClient } from "../../../shared/api/client";
import type { AuditLogPage, AuditLogQuery } from "../../../types";

export type AuditLogExportFormat = "csv" | "json";
export type AuditLogFilters = Omit<AuditLogQuery, "page" | "pageSize">;

export const auditLogService = {
  async getPage(query: AuditLogQuery): Promise<AuditLogPage> {
    const parameters = new URLSearchParams({
      page: String(query.page),
      pageSize: String(query.pageSize),
    });

    for (const [key, value] of Object.entries({
      search: query.search,
      action: query.action,
      environment: query.environment,
      dateFrom: query.dateFrom,
      dateTo: query.dateTo,
    })) {
      if (value) parameters.set(key, value);
    }

    return apiClient.get<AuditLogPage>(`/admin/audit-logs?${parameters.toString()}`);
  },

  async export(filters: AuditLogFilters, format: AuditLogExportFormat) {
    const parameters = new URLSearchParams({ format });
    for (const [key, value] of Object.entries(filters)) {
      if (value) parameters.set(key, value);
    }

    return apiClient.getBlob(`/admin/audit-logs/export?${parameters.toString()}`);
  },
};
