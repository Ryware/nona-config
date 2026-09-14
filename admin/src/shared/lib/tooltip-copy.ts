export const tooltipCopy = {
  scope:
    "Client is for non-sensitive frontend or mobile values. Server is backend-only. All is readable by both and should be used only when both need the value.",
  clientScope: "Readable by frontend and mobile consumers. Do not use for sensitive values.",
  serverScope: "Backend-only and unavailable to client-scoped API keys.",
  allScope: "Readable by both client- and server-scoped consumers.",
  datatype: "Controls how the value is validated: text, number, boolean, or structured JSON.",
  environment: "Separates configuration for a runtime stage such as staging or production.",
  activeEnvironment: "New parameters, keys, releases, and reads use the selected environment.",
  activeRelease: "The immutable snapshot returned to unversioned clients. Clear it to serve working configuration instead.",
  workingConfig: "The editable parameter set used to prepare the next release.",
  version: "Enter a major-minor version such as 1.2; Nona creates patch 1.2.0.",
  amend: "Creates the next patch from this release without changing working configuration.",
  apiKeyScope: "Limits which parameter scopes this key can read. Match it to the consuming application.",
  shareExpiration: "The link stops working automatically after this duration. Prefer the shortest useful lifetime.",
  sharePermission: "View only can read the value. Can edit may also update this single parameter.",
  viewer: "Can read project configuration, environments, and releases.",
  editor: "Can also change configuration, API keys, and parameter share links.",
  admin: "Has global project access plus user management and audit logs.",
  member: "Has access only to assigned projects, with Viewer or Editor permission per project.",
  auditContext: "Shows the project and environment affected by the recorded activity.",
} as const;

export function scopeTooltip(scope: string) {
  if (scope === "client") return tooltipCopy.clientScope;
  if (scope === "server") return tooltipCopy.serverScope;
  return tooltipCopy.allScope;
}
