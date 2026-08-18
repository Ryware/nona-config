export type SsoProviderName = "google" | "microsoft";

export interface SsoRedirectResult {
  provider: SsoProviderName;
  idToken?: string;
  error?: string;
}

interface StoredSsoRedirectContext {
  provider: SsoProviderName;
  returnPath: string;
  createdAt: number;
}

interface StoredSsoRedirectResult extends SsoRedirectResult {
  createdAt: number;
}

const CONTEXT_KEY_PREFIX = "nona:sso:redirect:context:";
const PENDING_KEY_PREFIX = "nona:sso:redirect:pending:";
const RESULT_KEY = "nona:sso:redirect:result";
const MAX_REDIRECT_AGE_MS = 10 * 60 * 1000;

export function createSsoFlowId() {
  const bytes = new Uint8Array(16);
  crypto.getRandomValues(bytes);
  return Array.from(bytes, byte => byte.toString(16).padStart(2, "0")).join("");
}

export function beginSsoRedirect(provider: SsoProviderName, flowId = createSsoFlowId()) {
  const context: StoredSsoRedirectContext = {
    provider,
    returnPath: getCurrentReturnPath(),
    createdAt: Date.now(),
  };

  sessionStorage.setItem(contextKey(flowId), JSON.stringify(context));
  sessionStorage.setItem(pendingKey(provider), flowId);
  sessionStorage.removeItem(RESULT_KEY);

  return flowId;
}

export function cancelSsoRedirect(provider: SsoProviderName, flowId: string) {
  sessionStorage.removeItem(contextKey(flowId));
  if (sessionStorage.getItem(pendingKey(provider)) === flowId) {
    sessionStorage.removeItem(pendingKey(provider));
  }
}

export function getPendingSsoFlow(provider: SsoProviderName) {
  const flowId = sessionStorage.getItem(pendingKey(provider));
  return isValidFlowId(flowId) ? flowId : null;
}

export function completeSsoRedirect(
  provider: SsoProviderName,
  flowId: string,
  result: Omit<SsoRedirectResult, "provider">,
) {
  const context = readContext(flowId);
  cancelSsoRedirect(provider, flowId);

  if (!context || context.provider !== provider) {
    return null;
  }

  const storedResult: StoredSsoRedirectResult = {
    provider,
    ...result,
    createdAt: Date.now(),
  };
  sessionStorage.setItem(RESULT_KEY, JSON.stringify(storedResult));

  return context.returnPath;
}

export function consumeSsoRedirectResult(): SsoRedirectResult | null {
  const raw = sessionStorage.getItem(RESULT_KEY);
  sessionStorage.removeItem(RESULT_KEY);

  if (!raw) {
    return null;
  }

  try {
    const result = JSON.parse(raw) as StoredSsoRedirectResult;
    if (
      !isProvider(result.provider) ||
      !isFresh(result.createdAt) ||
      (!result.idToken && !result.error)
    ) {
      return null;
    }

    return {
      provider: result.provider,
      idToken: result.idToken,
      error: result.error,
    };
  } catch {
    return null;
  }
}

export function isValidFlowId(value: string | null | undefined): value is string {
  return typeof value === "string" && /^[a-f0-9]{32}$/.test(value);
}

function readContext(flowId: string): StoredSsoRedirectContext | null {
  if (!isValidFlowId(flowId)) {
    return null;
  }

  const raw = sessionStorage.getItem(contextKey(flowId));
  if (!raw) {
    return null;
  }

  try {
    const context = JSON.parse(raw) as StoredSsoRedirectContext;
    if (
      !isProvider(context.provider) ||
      !isFresh(context.createdAt) ||
      !isAllowedReturnPath(context.returnPath)
    ) {
      return null;
    }

    return context;
  } catch {
    return null;
  }
}

function getCurrentReturnPath() {
  const path = `${window.location.pathname}${window.location.search}`;
  if (!isAllowedReturnPath(path)) {
    throw new Error("SSO can only be started from a supported sign-in page.");
  }

  return path;
}

function isAllowedReturnPath(path: string) {
  if (!path.startsWith("/") || path.startsWith("//") || path.includes("\\")) {
    return false;
  }

  const pathname = path.split("?")[0];
  return (
    pathname === "/login" ||
    pathname === "/cli-login" ||
    /^\/invite\/[^/]+$/.test(pathname)
  );
}

function isProvider(value: unknown): value is SsoProviderName {
  return value === "google" || value === "microsoft";
}

function isFresh(createdAt: unknown) {
  return (
    typeof createdAt === "number" &&
    createdAt <= Date.now() &&
    Date.now() - createdAt <= MAX_REDIRECT_AGE_MS
  );
}

function contextKey(flowId: string) {
  return `${CONTEXT_KEY_PREFIX}${flowId}`;
}

function pendingKey(provider: SsoProviderName) {
  return `${PENDING_KEY_PREFIX}${provider}`;
}
