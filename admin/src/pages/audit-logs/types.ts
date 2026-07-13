export interface AuditEntry {
  id: string;
  time: Date;
  actor: string;
  actorIconColor: string;
  actorTextColor: string;
  actorIsSystem: boolean;
  action: string;
  actionStyle: string;
  target: string;
  targetMono: boolean;
  targetDeleted: boolean;
  env: string;
  envStyle: string;
  sysId: string;
  actionCode?: string;
  ipAddress?: string;
  oldValue?: string;
  newValue?: string;
  contentType?: string;
  scope?: string;
  key?: string;
  project?: string;
  displayName?: string;
  description?: string;
  oldDisplayName?: string;
  oldDescription?: string;
}

export interface AuditLogEntry {
  id: string;
  time: string;
  actor: string;
  actionCode: "CREATE_ENTRY" | "UPDATE_ENTRY" | "DELETE_ENTRY" | "CREATE_PROJECT" | "UPDATE_PROJECT" | "INVITE_USER";
  ipAddress: string;
  sysId: string;
  project: string;
  environment: string;
  key: string;
  oldValue?: string;
  newValue?: string;
  contentType?: string;
  scope?: string;
  displayName?: string;
  description?: string;
  oldDisplayName?: string;
  oldDescription?: string;
}
