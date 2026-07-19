import { resolveNonNegativeNumber } from "./request-helpers.js";
import type { NonaConfigValue, NonaConfigValues } from "./types.js";

interface CacheEntry {
  expiresAt: number;
  sizeBytes: number;
  value: NonaConfigValue;
}

interface BulkCacheEntry {
  etag?: string;
  requestIds: Map<string, string>;
  sizeBytes: number;
  values: NonaConfigValues;
}

interface PrimedValue {
  bulkKey: string;
  valueKey: string;
}

interface ClientCacheOptions {
  ttlMs?: number;
  memoryLimitMegabytes?: number;
}

export interface CachedBulkValues {
  etag?: string;
  values: NonaConfigValues;
}

const DEFAULT_CACHE_MEMORY_LIMIT_MEGABYTES = 5;

export class ClientCache {
  readonly #ttlEnabled: boolean;
  readonly #ttlMs: number;
  readonly #memoryLimitBytes: number;
  readonly #ttlEntries = new Map<string, CacheEntry>();
  readonly #bulkEntries = new Map<string, BulkCacheEntry>();
  readonly #primedValues = new Map<string, PrimedValue>();
  readonly #lru = new Map<string, { kind: "ttl" | "bulk"; key: string }>();
  #sizeBytes = 0;

  constructor(options: ClientCacheOptions) {
    this.#ttlMs = resolveNonNegativeNumber(options.ttlMs ?? 0);
    this.#memoryLimitBytes =
      resolveNonNegativeNumber(
        options.memoryLimitMegabytes ?? DEFAULT_CACHE_MEMORY_LIMIT_MEGABYTES,
      ) *
      1024 *
      1024;
    this.#ttlEnabled = this.#ttlMs > 0;
  }

  getFresh(key: string): NonaConfigValue | undefined {
    const entry = this.#ttlEntries.get(key);
    if (!entry) {
      return undefined;
    }

    if (entry.expiresAt <= Date.now()) {
      this.#removeTtl(key);
      return undefined;
    }

    this.#touch("ttl", key);
    return cloneConfigValue(entry.value);
  }

  set(key: string, value: NonaConfigValue): void {
    if (!this.#ttlEnabled || this.#memoryLimitBytes <= 0) {
      return;
    }

    this.#removeTtl(key);
    const cachedValue = cloneConfigValue(value);
    const entry: CacheEntry = {
      value: cachedValue,
      expiresAt: Date.now() + this.#ttlMs,
      sizeBytes: estimateConfigValueSize(cachedValue),
    };

    this.#ttlEntries.set(key, entry);
    this.#sizeBytes += entry.sizeBytes;
    this.#touch("ttl", key);
    this.#evictToLimit();
  }

  getBulk(key: string): CachedBulkValues | undefined {
    const entry = this.#bulkEntries.get(key);
    if (!entry) {
      return undefined;
    }

    this.#touch("bulk", key);
    return entry;
  }

  setBulk(
    key: string,
    etag: string | undefined,
    values: NonaConfigValues,
    requestIds: Map<string, string>,
  ): void {
    this.#removeBulk(key);

    for (const requestId of requestIds.keys()) {
      this.#removeTtl(requestId);
    }

    if (this.#memoryLimitBytes <= 0) {
      return;
    }

    const cachedValues = cloneConfigValues(values);
    const cachedRequestIds = new Map(requestIds);
    const entry: BulkCacheEntry = {
      etag,
      requestIds: cachedRequestIds,
      sizeBytes: estimateBulkEntrySize(etag, cachedValues, cachedRequestIds),
      values: cachedValues,
    };

    this.#bulkEntries.set(key, entry);
    this.#sizeBytes += entry.sizeBytes;
    for (const [requestId, valueKey] of cachedRequestIds) {
      this.#primedValues.set(requestId, { bulkKey: key, valueKey });
    }

    this.#touch("bulk", key);
    this.#evictToLimit();
  }

  primeBulk(key: string): boolean {
    const entry = this.#bulkEntries.get(key);
    if (!entry) {
      return false;
    }

    for (const [requestId, valueKey] of entry.requestIds) {
      this.#primedValues.set(requestId, { bulkKey: key, valueKey });
    }

    this.#touch("bulk", key);
    return true;
  }

  getPrimed(key: string): NonaConfigValue | undefined {
    const primed = this.#primedValues.get(key);
    if (!primed) {
      return undefined;
    }

    const bulk = this.#bulkEntries.get(primed.bulkKey);
    const value = bulk?.values[primed.valueKey];
    if (!bulk || !value) {
      this.#primedValues.delete(key);
      return undefined;
    }

    this.#touch("bulk", primed.bulkKey);
    return cloneConfigValue(value);
  }

  invalidate(key: string): boolean {
    const ttlInvalidated = this.#removeTtl(key);
    const primedInvalidated = this.#primedValues.delete(key);
    return ttlInvalidated || primedInvalidated;
  }

  clear(): void {
    this.#ttlEntries.clear();
    this.#bulkEntries.clear();
    this.#primedValues.clear();
    this.#lru.clear();
    this.#sizeBytes = 0;
  }

  #evictToLimit(): void {
    while (this.#sizeBytes > this.#memoryLimitBytes && this.#lru.size > 0) {
      const oldest = this.#lru.values().next();
      if (oldest.done) {
        return;
      }

      if (oldest.value.kind === "ttl") {
        this.#removeTtl(oldest.value.key);
      } else {
        this.#removeBulk(oldest.value.key);
      }
    }
  }

  #removeTtl(key: string): boolean {
    const entry = this.#ttlEntries.get(key);
    if (!entry) {
      return false;
    }

    this.#ttlEntries.delete(key);
    this.#lru.delete(lruKey("ttl", key));
    this.#decreaseSize(entry.sizeBytes);
    return true;
  }

  #removeBulk(key: string): boolean {
    const entry = this.#bulkEntries.get(key);
    if (!entry) {
      return false;
    }

    this.#bulkEntries.delete(key);
    this.#lru.delete(lruKey("bulk", key));
    this.#decreaseSize(entry.sizeBytes);
    for (const requestId of entry.requestIds.keys()) {
      if (this.#primedValues.get(requestId)?.bulkKey === key) {
        this.#primedValues.delete(requestId);
      }
    }

    return true;
  }

  #touch(kind: "ttl" | "bulk", key: string): void {
    const id = lruKey(kind, key);
    this.#lru.delete(id);
    this.#lru.set(id, { kind, key });
  }

  #decreaseSize(sizeBytes: number): void {
    this.#sizeBytes -= sizeBytes;
    if (this.#sizeBytes < 0) {
      this.#sizeBytes = 0;
    }
  }
}

export function cloneConfigValues(values: NonaConfigValues): NonaConfigValues {
  const clone: NonaConfigValues = {};
  for (const [key, value] of Object.entries(values)) {
    Object.defineProperty(clone, key, {
      configurable: true,
      enumerable: true,
      value: cloneConfigValue(value),
      writable: true,
    });
  }

  return clone;
}

function cloneConfigValue(value: NonaConfigValue): NonaConfigValue {
  return { value: value.value, contentType: value.contentType };
}

function estimateBulkEntrySize(
  etag: string | undefined,
  values: NonaConfigValues,
  requestIds: Map<string, string>,
): number {
  let sizeBytes = roughByteLength(etag ?? "");
  for (const [key, value] of Object.entries(values)) {
    sizeBytes += roughByteLength(key) + estimateConfigValueSize(value);
  }

  for (const [requestId, valueKey] of requestIds) {
    sizeBytes += roughByteLength(requestId) + roughByteLength(valueKey);
  }

  return sizeBytes;
}

function estimateConfigValueSize(value: NonaConfigValue): number {
  return roughByteLength(value.value) + roughByteLength(value.contentType);
}

function roughByteLength(value: string): number {
  return value.length * 2;
}

function lruKey(kind: "ttl" | "bulk", key: string): string {
  return `${kind}\u0000${key}`;
}
