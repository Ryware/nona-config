/**
 * @deprecated LocalParamMetadataService is a client-side workaround for storing
 * parameter display names, descriptions, and revision history in localStorage.
 *
 * Limitations:
 * - Data is NOT synced across devices or browsers
 * - Subject to localStorage quota limits (~5 MB)
 * - No multi-user audit trail
 *
 * TODO: Replace with RemoteParamMetadataService once backend adds:
 *   - GET/PUT /admin/projects/:id/environments/:env/config-entries/:key/metadata
 *   - GET     /admin/projects/:id/environments/:env/config-entries/:key/revisions
 * Backend issue: Implement server-side param metadata and revision history.
 */

export interface ParamMeta {
  displayName?: string;
  description?: string;
}

export interface ParamRevision {
  timestamp: string;
  project: string;
  environment: string;
  key: string;
  value: string;
  actor: string;
  displayName?: string;
  description?: string;
}

/** Convert an env-key name like DATABASE_URL to "Database Url" */
export function autoFormatKey(key: string): string {
  return key
    .toLowerCase()
    .replace(/_/g, " ")
    .replace(/\b\w/g, (c) => c.toUpperCase());
}

const PRESET_METADATA: Record<string, ParamMeta> = {
  DATABASE_PORT: {
    displayName: "Database Port",
    description: "The port number of the main database instance.",
  },
  DATABASE_URL: {
    displayName: "Database URL",
    description: "The primary database connection string, including credentials.",
  },
  SMTP_SERVER_TLS_PORT: {
    displayName: "Mail Server Port (TLS)",
    description: "The port number used for sending secure emails via SMTP TLS.",
  },
  SMTP_PORT: {
    displayName: "Mail Server Port (Standard)",
    description: "Standard SMTP mail server port.",
  },
  SMTP_HOST: {
    displayName: "Mail Server Host",
    description: "The hostname or domain of the outgoing SMTP mail server.",
  },
  JWT_EXPIRY: {
    displayName: "Session Token Expiry",
    description: "The duration session JSON Web Tokens (JWT) remain valid (e.g. 24h, 7d).",
  },
  JWT_SECRET: {
    displayName: "Session Signature Secret",
    description: "The secret cryptographic key used to sign and verify user session tokens.",
  },
  PORT: {
    displayName: "Application Port",
    description: "The local network port on which the web server listens.",
  },
  NODE_ENV: {
    displayName: "Environment Mode",
    description: "Determines build optimizations and logs (production, staging, development).",
  },
  API_URL: {
    displayName: "API Base URL",
    description: "The root URL address of the backend service API.",
  },
  APP_ENV: {
    displayName: "App Deployment Mode",
    description: "Specifies which environment context the application runs in.",
  },
  LOG_LEVEL: {
    displayName: "System Log Detail Level",
    description: "Controls what logs are recorded (debug, info, warn, error).",
  },
};

/**
 * Client-side store for per-parameter display names, descriptions, and a local
 * revision log, persisted in `localStorage` keyed by `project:env:key`. Unknown
 * keys fall back to {@link PRESET_METADATA} / {@link autoFormatKey}.
 *
 * Exposed as a single shared instance ({@link localParamMetadataService}). Kept
 * as a class purely for that encapsulated-singleton shape and its private cache;
 * a future remote adapter can implement the same surface.
 *
 * @deprecated See the module-level deprecation notice above.
 */
class LocalParamMetadataService {
  private readonly metaKey = "nonaconfig_param_meta";
  private readonly historyKey = "nonaconfig_param_history";
  // In-memory cache. getMeta() runs per parameter row and inside the search
  // filter, so this avoids re-reading and JSON.parsing localStorage on every
  // call. Writes keep the cache and localStorage in sync.
  private metaCache: Record<string, ParamMeta> | null = null;
  private historyCache: ParamRevision[] | null = null;

  private loadMetaCache(): Record<string, ParamMeta> {
    if (this.metaCache) {
      return this.metaCache;
    }

    try {
      const raw = localStorage.getItem(this.metaKey);
      this.metaCache = raw ? (JSON.parse(raw) as Record<string, ParamMeta>) : {};
    } catch (e) {
      console.error("Failed to read param metadata", e);
      this.metaCache = {};
    }

    return this.metaCache;
  }

  private loadHistoryCache(): ParamRevision[] {
    if (this.historyCache) {
      return this.historyCache;
    }

    try {
      const raw = localStorage.getItem(this.historyKey);
      this.historyCache = raw ? (JSON.parse(raw) as ParamRevision[]) : [];
    } catch (e) {
      console.error("Failed to read param history", e);
      this.historyCache = [];
    }

    return this.historyCache;
  }

  /** Resolves a parameter's display name and description (stored → preset → auto-formatted). */
  getMeta(
    project: string,
    env: string,
    key: string,
  ): { displayName: string; description: string } {
    const dict = this.loadMetaCache();
    const keyPath = `${project}:${env}:${key}`;
    if (dict[keyPath]) {
      return {
        displayName: dict[keyPath].displayName || autoFormatKey(key),
        description:
          dict[keyPath].description || `Configuration setting for ${key}.`,
      };
    }

    const preset = PRESET_METADATA[key];
    return {
      displayName: preset?.displayName || autoFormatKey(key),
      description: preset?.description || `Configuration setting for ${key}.`,
    };
  }

  /** Persists (merges) display name / description overrides for a parameter. */
  setMeta(project: string, env: string, key: string, meta: ParamMeta): void {
    try {
      const dict = { ...this.loadMetaCache() };
      const keyPath = `${project}:${env}:${key}`;
      dict[keyPath] = { ...dict[keyPath], ...meta };
      this.metaCache = dict;
      localStorage.setItem(this.metaKey, JSON.stringify(dict));
    } catch (e) {
      console.error("Failed to save param metadata", e);
    }
  }

  /** Appends a local revision-log entry for a parameter change. */
  addRevision(
    project: string,
    env: string,
    key: string,
    value: string,
    actor: string,
    displayName?: string,
    description?: string,
  ): void {
    try {
      const history = [...this.loadHistoryCache()];
      history.push({
        timestamp: new Date().toISOString(),
        project,
        environment: env,
        key,
        value,
        actor,
        displayName,
        description,
      });
      this.historyCache = history;
      localStorage.setItem(this.historyKey, JSON.stringify(history));
    } catch (e) {
      console.error("Failed to write param history", e);
    }
  }

  /** Returns this parameter's local revisions, newest first. */
  getRevisions(project: string, env: string, key: string): ParamRevision[] {
    try {
      const history = this.loadHistoryCache();
      return history
        .filter(
          (h) => h.project === project && h.environment === env && h.key === key,
        )
        .sort(
          (a, b) =>
            new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime(),
        );
    } catch {
      return [];
    }
  }
}

/**
 * @deprecated Use localParamMetadataService only as a temporary workaround.
 * Replace with a remote adapter once the backend provides the corresponding endpoints.
 */
export const localParamMetadataService = new LocalParamMetadataService();
