import {
  ErrorCode,
  StandardResolutionReasons,
  type EvaluationContext,
  type JsonValue,
  type Logger,
  type Provider,
  type ResolutionDetails,
} from "@openfeature/server-sdk";
import {
  createNonaClient,
  NonaClientError,
  type NonaClient,
  type NonaClientOptions,
  type NonaConfigValue,
} from "nona-client";

export interface NonaOpenFeatureProviderSettings {
  metadataName?: string;
}

export type NonaOpenFeatureProviderOptions = NonaClientOptions &
  NonaOpenFeatureProviderSettings;

export function createNonaOpenFeatureProvider(
  client: NonaClient,
  settings?: NonaOpenFeatureProviderSettings,
): Provider;
export function createNonaOpenFeatureProvider(
  options: NonaOpenFeatureProviderOptions,
): Provider;
export function createNonaOpenFeatureProvider(
  clientOrOptions: NonaClient | NonaOpenFeatureProviderOptions,
  settings: NonaOpenFeatureProviderSettings | string = {},
): Provider {
  if ("getConfigValue" in clientOrOptions && typeof settings === "string") {
    throw new Error(
      "createNonaOpenFeatureProvider reads environmentId from the Nona client. Pass environmentId to createNonaClient instead.",
    );
  }

  const client =
    "getConfigValue" in clientOrOptions
      ? clientOrOptions
      : createNonaClient(clientOrOptions);
  const metadataName =
    "getConfigValue" in clientOrOptions
      ? (settings as NonaOpenFeatureProviderSettings).metadataName
      : clientOrOptions.metadataName;

  return new NonaOpenFeatureProvider(client, metadataName);
}

export class NonaOpenFeatureProvider implements Provider {
  readonly metadata;
  readonly runsOn = "server" as const;

  constructor(
    private readonly client: NonaClient,
    metadataName = "nona",
  ) {
    this.metadata = { name: metadataName };
  }

  async resolveBooleanEvaluation(
    flagKey: string,
    defaultValue: boolean,
    _context: EvaluationContext,
    _logger: Logger,
  ): Promise<ResolutionDetails<boolean>> {
    return this.resolveFlag(flagKey, defaultValue, (config) => {
      const raw = config.value.trim().toLowerCase();
      if (raw === "true") {
        return success(flagKey, true, config);
      }

      if (raw === "false") {
        return success(flagKey, false, config);
      }

      return error(
        flagKey,
        defaultValue,
        ErrorCode.TYPE_MISMATCH,
        `Nona flag '${flagKey}' cannot be evaluated as a boolean.`,
      );
    });
  }

  async resolveStringEvaluation(
    flagKey: string,
    defaultValue: string,
    _context: EvaluationContext,
    _logger: Logger,
  ): Promise<ResolutionDetails<string>> {
    return this.resolveFlag(flagKey, defaultValue, (config) =>
      success(flagKey, config.value, config),
    );
  }

  async resolveNumberEvaluation(
    flagKey: string,
    defaultValue: number,
    _context: EvaluationContext,
    _logger: Logger,
  ): Promise<ResolutionDetails<number>> {
    return this.resolveFlag(flagKey, defaultValue, (config) => {
      const value = Number(config.value);
      if (config.value.trim() === "" || !Number.isFinite(value)) {
        return error(
          flagKey,
          defaultValue,
          ErrorCode.TYPE_MISMATCH,
          `Nona flag '${flagKey}' cannot be evaluated as a number.`,
        );
      }

      return success(flagKey, value, config);
    });
  }

  async resolveObjectEvaluation<T extends JsonValue>(
    flagKey: string,
    defaultValue: T,
    _context: EvaluationContext,
    _logger: Logger,
  ): Promise<ResolutionDetails<T>> {
    return this.resolveFlag(flagKey, defaultValue, (config) => {
      try {
        return success(flagKey, JSON.parse(config.value) as T, config);
      } catch {
        return error(
          flagKey,
          defaultValue,
          ErrorCode.PARSE_ERROR,
          `Nona flag '${flagKey}' cannot be parsed as JSON.`,
        );
      }
    });
  }

  private async resolveFlag<T>(
    flagKey: string,
    defaultValue: T,
    resolve: (config: NonaConfigValue) => ResolutionDetails<T>,
  ): Promise<ResolutionDetails<T>> {
    try {
      return resolve(await this.client.getConfigValue(flagKey));
    } catch (cause) {
      if (cause instanceof NonaClientError && cause.status === 404) {
        return error(
          flagKey,
          defaultValue,
          ErrorCode.FLAG_NOT_FOUND,
          cause.message,
        );
      }

      return error(
        flagKey,
        defaultValue,
        ErrorCode.GENERAL,
        cause instanceof Error ? cause.message : String(cause),
      );
    }
  }
}

function success<T>(
  flagKey: string,
  value: T,
  config: NonaConfigValue,
): ResolutionDetails<T> {
  return {
    value,
    reason: StandardResolutionReasons.STATIC,
    flagMetadata: {
      contentType: config.contentType,
      nonaKey: flagKey,
    },
  };
}

function error<T>(
  flagKey: string,
  defaultValue: T,
  errorCode: ErrorCode,
  errorMessage: string,
): ResolutionDetails<T> {
  return {
    value: defaultValue,
    reason: StandardResolutionReasons.ERROR,
    errorCode,
    errorMessage,
    flagMetadata: {
      nonaKey: flagKey,
    },
  };
}
