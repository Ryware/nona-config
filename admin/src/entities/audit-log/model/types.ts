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
