import { resolveNonNegativeNumber } from "./request-helpers.js";
import type { NonaConfigValue } from "./types.js";

interface CacheEntry {
  expiresAt: number;
  sizeBytes: number;
  value: NonaConfigValue;
}

interface TtlCacheOptions {
  ttlMs?: number;
  memoryLimitMegabytes?: number;
}

const DEFAULT_CACHE_MEMORY_LIMIT_MEGABYTES = 5;

export class TtlCache {
  readonly #enabled: boolean;
  readonly #ttlMs: number;
  readonly #memoryLimitBytes: number;
  readonly #entries = new Map<string, CacheEntry>();
  #sizeBytes = 0;

  constructor(options: TtlCacheOptions) {
    this.#ttlMs = resolveNonNegativeNumber(options.ttlMs ?? 0);
    this.#memoryLimitBytes =
      resolveNonNegativeNumber(
        options.memoryLimitMegabytes ?? DEFAULT_CACHE_MEMORY_LIMIT_MEGABYTES,
      ) *
      1024 *
      1024;
    this.#enabled = this.#ttlMs > 0;
  }

  get enabled(): boolean {
    return this.#enabled;
  }

  getFresh(key: string): NonaConfigValue | undefined {
    const entry = this.#entries.get(key);
    if (!entry) {
      return undefined;
    }

    if (entry.expiresAt <= Date.now()) {
      return undefined;
    }

    // Reinsert so eviction behaves like LRU.
    this.#entries.delete(key);
    this.#entries.set(key, entry);
    return entry.value;
  }

  set(key: string, value: NonaConfigValue): void {
    if (!this.#enabled || this.#memoryLimitBytes <= 0) {
      return;
    }

    const entry: CacheEntry = {
      value,
      expiresAt: Date.now() + (this.#ttlMs),
      sizeBytes: estimateConfigValueSize(value),
    };

    const existing = this.#entries.get(key);
    if (existing) {
      this.#sizeBytes -= existing.sizeBytes;
      this.#entries.delete(key);
    }

    this.#entries.set(key, entry);
    this.#sizeBytes += entry.sizeBytes;

    while (this.#sizeBytes > this.#memoryLimitBytes && this.#entries.size > 0) {
      this.evictOldest();
    }
  }

  invalidate(key: string): boolean {
    const existing = this.#entries.get(key);
    if (!existing) {
      return false;
    }

    this.#entries.delete(key);
    this.#sizeBytes -= existing.sizeBytes;
    if (this.#sizeBytes < 0) {
      this.#sizeBytes = 0;
    }

    return true;
  }

  clear(): void {
    this.#entries.clear();
    this.#sizeBytes = 0;
  }

  evictOldest(): void {
    const oldest = this.#entries.keys().next();
    if (oldest.done) {
      return;
    }

    const entry = this.#entries.get(oldest.value);
    if (entry) {
      this.#sizeBytes -= entry.sizeBytes;
    }

    this.#entries.delete(oldest.value);
  }
}

function estimateConfigValueSize(value: NonaConfigValue): number {
  return roughByteLength(value.value) + roughByteLength(value.contentType);
}

function roughByteLength(value: string): number {
  return value.length * 2;
}
