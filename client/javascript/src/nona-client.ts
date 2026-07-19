import { applyAuthentication } from "./auth.js";
import { NonaClientError } from "./errors.js";
import {
  buildRequestKey,
  ensureTrailingSlash,
  segment,
} from "./request-helpers.js";
import {
  readAllConfigValuesResponse,
  readRawEntryValueResponse,
} from "./response-helpers.js";
import { ClientCache, cloneConfigValues } from "./client-cache.js";
import type {
  NonaClientOptions,
  NonaConfigValues,
  NonaConfigValue,
  NonaRequestOptions,
} from "./types.js";

interface SendOptions extends NonaRequestOptions {
  body?: unknown;
  headers?: HeadersInit;
  method: string;
  path: string;
}

export interface NonaClient {
  readonly environmentId: string;
  getConfigValue(
    key: string,
    options?: NonaRequestOptions,
  ): Promise<NonaConfigValue>;
  getAllValues(options?: NonaRequestOptions): Promise<NonaConfigValues>;
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

  const cache = new ClientCache({
    ttlMs: resolvedOptions.cacheTtlMs,
    memoryLimitMegabytes: resolvedOptions.cacheMemoryLimitMegabytes,
  });
  const pendingRequests = new Map<string, Promise<NonaConfigValue>>();
  const pendingBulkRequests = new Map<string, Promise<NonaConfigValues>>();
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

  function allConfigValuesPath(releaseVersion: string | undefined): string {
    const path = `api/${environmentSegment}`;
    if (!releaseVersion) {
      return path;
    }

    const search = new URLSearchParams();
    search.set("version", releaseVersion);
    return `${path}?${search.toString()}`;
  }

  function configValueRequestId(
    key: string,
    releaseVersion: string | undefined,
  ): string {
    return buildRequestKey(
      baseUrl,
      "GET",
      configValuePath(key, releaseVersion),
      apiKey,
    );
  }

  async function sendRequest(request: SendOptions): Promise<Response> {
    const url = new URL(request.path.replace(/^\/+/, ""), baseUrl).toString();
    const headers = new Headers(defaultHeaders);
    new Headers(request.headers).forEach((value, key) => {
      headers.set(key, value);
    });
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

      const primed = cache.getPrimed(id);
      if (primed) {
        return primed;
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
    async getAllValues(
      requestOptions: NonaRequestOptions = {},
    ): Promise<NonaConfigValues> {
      const releaseVersion =
        requestOptions.releaseVersion ?? defaultReleaseVersion;
      const path = allConfigValuesPath(releaseVersion);
      const id = buildRequestKey(baseUrl, "GET", path, apiKey);

      const pending = pendingBulkRequests.get(id);
      if (pending) {
        return cloneConfigValues(await pending);
      }

      const previous = cache.getBulk(id);
      const request: SendOptions = {
        method: "GET",
        path,
        ...requestOptions,
        headers: previous?.etag
          ? { "If-None-Match": previous.etag }
          : undefined,
      };

      const inFlight = sendRequest(request)
        .then(async (response) => {
          if (response.status === 304 && previous) {
            cache.primeBulk(id);
            return previous.values;
          }

          const values = await readAllConfigValuesResponse(
            response,
            request.method,
            response.url,
          );
          cache.setBulk(
            id,
            response.headers.get("ETag") ?? undefined,
            values,
            configValueRequestIds(values, releaseVersion),
          );
          return values;
        })
        .finally(() => {
          pendingBulkRequests.delete(id);
        });

      pendingBulkRequests.set(id, inFlight);
      return cloneConfigValues(await inFlight);
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

  function configValueRequestIds(
    values: NonaConfigValues,
    releaseVersion: string | undefined,
  ): Map<string, string> {
    const valueRequestIds = new Map<string, string>();
    for (const key of Object.keys(values)) {
      const valueRequestId = configValueRequestId(key, releaseVersion);
      valueRequestIds.set(valueRequestId, key);
    }

    return valueRequestIds;
  }
}
