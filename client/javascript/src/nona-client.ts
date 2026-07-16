import { applyAuthentication } from "./auth.js";
import { NonaClientError } from "./errors.js";
import {
  buildRequestKey,
  ensureTrailingSlash,
  segment,
} from "./request-helpers.js";
import { readRawEntryValueResponse } from "./response-helpers.js";
import { TtlCache } from "./ttl-cache.js";
import type {
  NonaClientOptions,
  NonaConfigValue,
  NonaRequestOptions,
} from "./types.js";

interface SendOptions extends NonaRequestOptions {
  body?: unknown;
  method: string;
  path: string;
}

export interface NonaClient {
  readonly environmentId: string;
  getConfigValue(
    key: string,
    options?: NonaRequestOptions,
  ): Promise<NonaConfigValue>;
  tryGetConfigValue(
    key: string,
    options?: NonaRequestOptions,
  ): Promise<NonaConfigValue | null>;
  getStringValue(key: string, options?: NonaRequestOptions): Promise<string>;
  getJsonValue<T>(key: string, options?: NonaRequestOptions): Promise<T>;
  invalidateTtlCache(key: string, options?: NonaRequestOptions): boolean;
  clearTtlCache(): void;
}

export function createNonaClient(
  baseUrl: string | URL,
  options: Omit<NonaClientOptions, "baseUrl">,
): NonaClient;
export function createNonaClient(options: NonaClientOptions): NonaClient;
export function createNonaClient(
  baseUrlOrOptions: string | URL | NonaClientOptions,
  options?: Omit<NonaClientOptions, "baseUrl">,
): NonaClient {
  const resolvedOptions: NonaClientOptions =
    typeof baseUrlOrOptions === "string" || baseUrlOrOptions instanceof URL
      ? ({ ...options, baseUrl: baseUrlOrOptions } as NonaClientOptions)
      : baseUrlOrOptions;

  const baseUrl = ensureTrailingSlash(new URL(resolvedOptions.baseUrl));
  const environmentId = resolvedOptions.environmentId;
  const environmentSegment = segment(environmentId, "environmentId");
  const defaultReleaseVersion = resolvedOptions.releaseVersion;
  const defaultHeaders = resolvedOptions.defaultHeaders;
  const fetchImpl = resolvedOptions.fetch ?? globalThis.fetch?.bind(globalThis);

  const cache = new TtlCache({
    ttlMs: resolvedOptions.cacheTtlMs,
    memoryLimitMegabytes: resolvedOptions.cacheMemoryLimitMegabytes,
  });
  const pendingRequests = new Map<string, Promise<NonaConfigValue>>();
  const apiKey = resolvedOptions.apiKey;

  if (!fetchImpl) {
    throw new Error("createNonaClient requires a fetch implementation.");
  }

  async function sendConfigValue(request: SendOptions): Promise<NonaConfigValue> {
    const response = await sendRequest(request);
    return readRawEntryValueResponse(response, request.method, response.url);
  }

  function configValuePath(key: string, releaseVersion: string | undefined): string {
    const path = `api/${environmentSegment}/${segment(key, "key")}`;
    if (!releaseVersion) {
      return path;
    }

    const search = new URLSearchParams();
    search.set("version", releaseVersion);
    return `${path}?${search.toString()}`;
  }

  async function sendRequest(request: SendOptions): Promise<Response> {
    const url = new URL(request.path.replace(/^\/+/, ""), baseUrl).toString();
    const headers = new Headers(defaultHeaders);
    headers.set("Accept", "application/json");
    applyAuthentication(headers, apiKey);

    let body: string | undefined;
    if (request.body !== undefined) {
      headers.set("Content-Type", "application/json");
      body = JSON.stringify(request.body);
    }

    return fetchImpl(url, {
      method: request.method,
      headers,
      body,
      signal: request.signal,
    });
  }

  return {
    environmentId,
    async getConfigValue(
      key: string,
      requestOptions: NonaRequestOptions = {},
    ): Promise<NonaConfigValue> {
      const request: SendOptions = {
        method: "GET",
        path: configValuePath(
          key,
          requestOptions.releaseVersion ?? defaultReleaseVersion,
        ),
        ...requestOptions,
      };
      const id = buildRequestKey(baseUrl, request.method, request.path, apiKey);

      const cached = cache.getFresh(id);
      if (cached) {
        return cached;
      }

      const pending = pendingRequests.get(id);
      if (pending) {
        return pending;
      }

      const inFlight = sendConfigValue(request)
        .then((response) => {
          cache.set(id, response);
          return response;
        })
        .finally(() => {
          pendingRequests.delete(id);
        });

      pendingRequests.set(id, inFlight);
      return inFlight;
    },
    invalidateTtlCache(
      key: string,
      requestOptions: NonaRequestOptions = {},
    ): boolean {
      const request: SendOptions = {
        method: "GET",
        path: configValuePath(
          key,
          requestOptions.releaseVersion ?? defaultReleaseVersion,
        ),
      };
      const id = buildRequestKey(baseUrl, request.method, request.path, apiKey);
      return cache.invalidate(id);
    },
    clearTtlCache(): void {
      cache.clear();
    },
    async tryGetConfigValue(
      key: string,
      requestOptions: NonaRequestOptions = {},
    ): Promise<NonaConfigValue | null> {
      try {
        return await this.getConfigValue(key, requestOptions);
      } catch (error) {
        if (error instanceof NonaClientError && error.status === 404) {
          return null;
        }

        throw error;
      }
    },
    async getStringValue(
      key: string,
      requestOptions: NonaRequestOptions = {},
    ): Promise<string> {
      const configValue = await this.getConfigValue(
        key,
        requestOptions,
      );
      return configValue.value;
    },
    async getJsonValue<T>(
      key: string,
      requestOptions: NonaRequestOptions = {},
    ): Promise<T> {
      const configValue = await this.getConfigValue(
        key,
        requestOptions,
      );
      return JSON.parse(configValue.value) as T;
    },
  };
}
