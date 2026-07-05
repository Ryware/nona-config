// Audit log domain types

export interface AuditLog {
  id: string;
  actor: string;
  actorIsSystem: boolean;
  action: string;
  target: string;
  project: string | null;
  environment: string | null;
  createdAt: string;
}
