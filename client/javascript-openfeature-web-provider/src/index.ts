import {
  ErrorCode,
  OpenFeatureEventEmitter,
  ProviderEvents,
  ProviderFatalError,
  StandardResolutionReasons,
  type FlagMetadata,
  type JsonValue,
  type Logger,
  type Provider,
  type ResolutionDetails,
} from "@openfeature/web-sdk";
import {
  createNonaClient,
  NonaClientError,
  type NonaClient,
  type NonaClientOptions,
  type NonaConfigValue,
  type NonaConfigValues,
} from "nona-client";
import {
  parseBooleanValue,
  parseNumberValue,
  parseObjectValue,
  parseStringValue,
  type ParseResult,
} from "./value-parsing.js";

const DEFAULT_POLL_INTERVAL_MS = 30_000;

export interface NonaOpenFeatureWebProviderSettings {
  /** OpenFeature provider metadata name. */
  metadataName?: string;
  /** Restrict the snapshot to keys under this prefix, e.g. `Features:`. */
  prefix?: string;
  /** Poll interval in ms; `0` disables polling. @default 30000 */
  pollIntervalMs?: number;
  /** Reports failed background refreshes, which keep the last good snapshot. */
  logger?: Logger;
}

export type NonaOpenFeatureWebProviderOptions = NonaClientOptions &
  NonaOpenFeatureWebProviderSettings;

export function createNonaOpenFeatureWebProvider(
  client: NonaClient,
  settings?: NonaOpenFeatureWebProviderSettings,
): NonaOpenFeatureWebProvider;
export function createNonaOpenFeatureWebProvider(
  options: NonaOpenFeatureWebProviderOptions,
): NonaOpenFeatureWebProvider;
export function createNonaOpenFeatureWebProvider(
  clientOrOptions: NonaClient | NonaOpenFeatureWebProviderOptions,
  settings: NonaOpenFeatureWebProviderSettings = {},
): NonaOpenFeatureWebProvider {
  if (isNonaClient(clientOrOptions)) {
    return new NonaOpenFeatureWebProvider(clientOrOptions, settings);
  }

  const { metadataName, prefix, pollIntervalMs, logger, ...clientOptions } =
    clientOrOptions;
  return new NonaOpenFeatureWebProvider(createNonaClient(clientOptions), {
    metadataName,
    prefix,
    pollIntervalMs,
    logger,
  });
}

/**
 * OpenFeature provider for the static-context (client) paradigm: loads the
 * environment's frontend-scoped config as one snapshot and resolves from
 * memory, which is what lets the web SDK evaluate synchronously.
 */
export class NonaOpenFeatureWebProvider implements Provider {
  readonly metadata;
  readonly runsOn = "client" as const;
  readonly events = new OpenFeatureEventEmitter();

  private values: NonaConfigValues | undefined;
  private timer: ReturnType<typeof setInterval> | undefined;

  constructor(
    private readonly client: NonaClient,
    private readonly settings: NonaOpenFeatureWebProviderSettings = {},
  ) {
    this.metadata = { name: settings.metadataName ?? "nona" };
  }

  async initialize(): Promise<void> {
    this.values = await this.fetchValues();
    this.startPolling();
  }

  /** Snapshots are the same for every caller today, so this is just a refetch. */
  async onContextChange(): Promise<void> {
    this.values = await this.fetchValues();
  }

  /** Refetch, emit `PROVIDER_CONFIGURATION_CHANGED`, return the changed keys. */
  async refresh(): Promise<string[]> {
    const next = await this.fetchValues();
    const previous = this.values;
    this.values = next;

    const flagsChanged = previous
      ? changedFlagKeys(previous, next)
      : Object.keys(next);
    if (flagsChanged.length > 0) {
      this.events.emit(ProviderEvents.ConfigurationChanged, { flagsChanged });
    }

    return flagsChanged;
  }

  async onClose(): Promise<void> {
    if (this.timer !== undefined) {
      clearInterval(this.timer);
      this.timer = undefined;
    }

    this.values = undefined;
  }

  resolveBooleanEvaluation(
    flagKey: string,
    defaultValue: boolean,
  ): ResolutionDetails<boolean> {
    return this.resolveFlag(flagKey, defaultValue, parseBooleanValue);
  }

  resolveStringEvaluation(
    flagKey: string,
    defaultValue: string,
  ): ResolutionDetails<string> {
    return this.resolveFlag(flagKey, defaultValue, parseStringValue);
  }

  resolveNumberEvaluation(
    flagKey: string,
    defaultValue: number,
  ): ResolutionDetails<number> {
    return this.resolveFlag(flagKey, defaultValue, parseNumberValue);
  }

  resolveObjectEvaluation<T extends JsonValue>(
    flagKey: string,
    defaultValue: T,
  ): ResolutionDetails<T> {
    return this.resolveFlag(flagKey, defaultValue, parseObjectValue<T>);
  }

  private resolveFlag<T>(
    flagKey: string,
    defaultValue: T,
    parse: (flagKey: string, config: NonaConfigValue) => ParseResult<T>,
  ): ResolutionDetails<T> {
    const values = this.values;
    if (!values) {
      return error(
        flagKey,
        defaultValue,
        ErrorCode.PROVIDER_NOT_READY,
        "The Nona web provider has not loaded a config snapshot yet.",
      );
    }

    const config = valueAt(values, flagKey);
    if (!config) {
      return error(
        flagKey,
        defaultValue,
        ErrorCode.FLAG_NOT_FOUND,
        `Nona flag '${flagKey}' is not in the '${this.client.environmentId}' snapshot. Client-side evaluation only sees frontend-scoped entries.`,
      );
    }

    const parsed = parse(flagKey, config);
    if (!parsed.ok) {
      return error(
        flagKey,
        defaultValue,
        parsed.kind === "parse-error"
          ? ErrorCode.PARSE_ERROR
          : ErrorCode.TYPE_MISMATCH,
        parsed.message,
        config,
      );
    }

    return success(flagKey, parsed.value, config);
  }

  private async fetchValues(): Promise<NonaConfigValues> {
    try {
      return await this.client.getAllValues({ prefix: this.settings.prefix });
    } catch (cause) {
      throw toSnapshotError(cause);
    }
  }

  private startPolling(): void {
    const interval = this.settings.pollIntervalMs ?? DEFAULT_POLL_INTERVAL_MS;
    if (!Number.isFinite(interval) || interval <= 0) {
      return;
    }

    this.timer = setInterval(() => {
      void this.refresh().catch((cause: unknown) => {
        this.settings.logger?.error(
          `Nona web provider could not refresh its config snapshot: ${describe(cause)}`,
        );
      });
    }, interval);

    // Do not hold a Node event loop open.
    (this.timer as unknown as { unref?: () => void }).unref?.();
  }
}

/**
 * 404 is ambiguous on purpose: the snapshot endpoint reports an unknown
 * environment and a backend-only key identically, so a server key cannot
 * enumerate environments. Neither it nor 401 is worth retrying.
 */
function toSnapshotError(cause: unknown): unknown {
  if (!(cause instanceof NonaClientError)) {
    return cause;
  }

  if (cause.status === 401) {
    return new ProviderFatalError(
      `Nona rejected the API key while loading the config snapshot: ${cause.message}`,
      { cause },
    );
  }

  if (cause.status === 404) {
    return new ProviderFatalError(
      `Nona could not return a config snapshot: ${cause.message}. Check the environment id, and that the API key is frontend-scoped — the snapshot endpoint is hidden from backend-only keys.`,
      { cause },
    );
  }

  return cause;
}

function flagMetadata(
  flagKey: string,
  config?: NonaConfigValue,
): FlagMetadata {
  return config
    ? { contentType: config.contentType, nonaKey: flagKey }
    : { nonaKey: flagKey };
}

/** Flag keys are arbitrary, so `toString` must not resolve off the prototype. */
function valueAt(
  values: NonaConfigValues,
  key: string,
): NonaConfigValue | undefined {
  return Object.hasOwn(values, key) ? values[key] : undefined;
}

function changedFlagKeys(
  previous: NonaConfigValues,
  next: NonaConfigValues,
): string[] {
  const keys = new Set([...Object.keys(previous), ...Object.keys(next)]);
  return [...keys].filter((key) => {
    const before = valueAt(previous, key);
    const after = valueAt(next, key);
    return (
      before?.value !== after?.value ||
      before?.contentType !== after?.contentType
    );
  });
}

function isNonaClient(
  value: NonaClient | NonaOpenFeatureWebProviderOptions,
): value is NonaClient {
  return "getConfigValue" in value;
}

function describe(cause: unknown): string {
  return cause instanceof Error ? cause.message : String(cause);
}

function success<T>(
  flagKey: string,
  value: T,
  config: NonaConfigValue,
): ResolutionDetails<T> {
  return {
    value,
    reason: StandardResolutionReasons.STATIC,
    flagMetadata: flagMetadata(flagKey, config),
  };
}

function error<T>(
  flagKey: string,
  defaultValue: T,
  errorCode: ErrorCode,
  errorMessage: string,
  config?: NonaConfigValue,
): ResolutionDetails<T> {
  return {
    value: defaultValue,
    reason: StandardResolutionReasons.ERROR,
    errorCode,
    errorMessage,
    flagMetadata: flagMetadata(flagKey, config),
  };
}
