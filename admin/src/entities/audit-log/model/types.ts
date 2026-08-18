// Audit log domain types

export type AuditActionKind = "create" | "update" | "delete" | "activity";

export interface AuditLog {
  id: string;
  actor: string;
  actorIsSystem: boolean;
  actionKind: AuditActionKind;
  action: string;
  target: string;
  project: string | null;
  environment: string | null;
  createdAt: string;
}

export interface AuditLogQuery {
  page: number;
  pageSize: number;
  search?: string;
  action?: string;
  environment?: string;
  dateFrom?: string;
  dateTo?: string;
}

export interface AuditLogPage {
  items: AuditLog[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  actions: string[];
  environments: string[];
}
