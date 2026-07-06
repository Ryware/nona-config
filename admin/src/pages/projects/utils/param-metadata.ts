export interface ParamMeta {
  displayName?: string;
  description?: string;
}

// Local helper to format key name into readable title-case display name
export function autoFormatKey(key: string): string {
  return key
    .toLowerCase()
    .replace(/_/g, " ")
    .replace(/\b\w/g, (c) => c.toUpperCase());
}

// Standard fallback descriptions for developer keys
export const PRESET_METADATA: Record<string, ParamMeta> = {
  DATABASE_PORT: {
    displayName: "Database Port",
    description: "The port number of the main database instance.",
  },
  DATABASE_URL: {
    displayName: "Database URL",
    description:
      "The primary database connection string, including credentials.",
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
    description:
      "The duration session JSON Web Tokens (JWT) remain valid (e.g. 24h, 7d).",
  },
  JWT_SECRET: {
    displayName: "Session Signature Secret",
    description:
      "The secret cryptographic key used to sign and verify user session tokens.",
  },
  PORT: {
    displayName: "Application Port",
    description: "The local network port on which the web server listens.",
  },
  NODE_ENV: {
    displayName: "Environment Mode",
    description:
      "Determines build optimizations and logs (production, staging, development).",
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

export function getParamMeta(
  proj: string,
  env: string,
  key: string,
): { displayName: string; description: string } {
  try {
    const raw = localStorage.getItem("nonaconfig_param_meta");
    if (raw) {
      const dict = JSON.parse(raw);
      const keyPath = `${proj}:${env}:${key}`;
      if (dict[keyPath]) {
        return {
          displayName: dict[keyPath].displayName || autoFormatKey(key),
          description:
            dict[keyPath].description || `Configuration setting for ${key}.`,
        };
      }
    }
  } catch (e) {
    console.error("Failed to read metadata", e);
  }

  const preset = PRESET_METADATA[key];
  return {
    displayName: preset?.displayName || autoFormatKey(key),
    description: preset?.description || `Configuration setting for ${key}.`,
  };
}

export function setParamMeta(proj: string, env: string, key: string, meta: ParamMeta) {
  try {
    const raw = localStorage.getItem("nonaconfig_param_meta") || "{}";
    const dict = JSON.parse(raw);
    const keyPath = `${proj}:${env}:${key}`;
    dict[keyPath] = { ...dict[keyPath], ...meta };
    localStorage.setItem("nonaconfig_param_meta", JSON.stringify(dict));
  } catch (e) {
    console.error("Failed to save metadata", e);
  }
}
