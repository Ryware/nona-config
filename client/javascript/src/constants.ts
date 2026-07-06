export const NonaContentTypes = {
  Text: "text",
  String: "text",
  Number: "number",
  Boolean: "boolean",
  Json: "json"
} as const;

export const NonaConfigScopes = {
  Client: "client",
  Server: "server",
  All: "all"
} as const;

export const NonaUserRoles = {
  Viewer: "viewer",
  Editor: "editor"
} as const;
