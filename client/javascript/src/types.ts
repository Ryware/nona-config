export type NonaContentType = "text" | "number" | "boolean" | "json";
export type NonaConfigScope = "client" | "server" | "all";
export type NonaUserRole = "viewer" | "editor";

export interface NonaClientOptions {
  baseUrl: string | URL;
  environmentId: string;
  apiKey?: string;
  releaseVersion?: string;
  fetch?: typeof fetch;
  defaultHeaders?: HeadersInit;
  cacheTtlMs?: number;
  cacheMemoryLimitMegabytes?: number;
}

export interface NonaRequestOptions {
  signal?: AbortSignal;
  releaseVersion?: string;
}

export interface NonaConfigValue {
  value: string;
  contentType: string;
}

export type NonaConfigValues = Record<string, NonaConfigValue>;

export interface NonaConfigEntry {
  project: string;
  environment: string;
  key: string;
  value: string;
  contentType: NonaContentType | string;
  scope: NonaConfigScope | string;
  createdAt: string;
  updatedAt: string;
}
