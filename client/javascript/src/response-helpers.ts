import { NonaClientError } from "./errors.js";
import type { NonaConfigValue } from "./types.js";

const contentTypeHeaderName = "X-Nona-Content-Type";
const legacyContentTypeHeaderName = "ContentType";

export async function readRawEntryValueResponse(
  response: Response,
  method: string,
  url: string,
): Promise<NonaConfigValue> {
  const responseBody = await response.text();

  if (!response.ok) {
    throwResponseError(response, method, url, responseBody);
  }

  const contentType =
    response.headers.get(contentTypeHeaderName) ??
    response.headers.get(legacyContentTypeHeaderName);
  if (contentType) {
    return {
      value: responseBody,
      contentType: normalizeContentType(contentType),
    };
  }

  if (responseBody.trim()) {
    try {
      return parseLegacyConfigValue(responseBody);
    } catch {
      return {
        value: responseBody,
        contentType: inferContentType(responseBody),
      };
    }
  }

  return {
    value: "",
    contentType: "text",
  };
}

export async function readJsonResponse<T>(
  response: Response,
  method: string,
  url: string,
): Promise<T> {
  const responseBody = await response.text();

  if (!response.ok) {
    throwResponseError(response, method, url, responseBody);
  }

  if (!responseBody.trim()) {
    throw new NonaClientError(
      "Nona returned an empty response body.",
      response.status,
      method,
      url,
      responseBody,
    );
  }

  try {
    return JSON.parse(responseBody) as T;
  } catch (error) {
    throw new NonaClientError(
      "Nona returned a response that could not be deserialized.",
      response.status,
      method,
      url,
      responseBody,
      error,
    );
  }
}

function throwResponseError(
  response: Response,
  method: string,
  url: string,
  responseBody: string,
): never {
  const message =
    readErrorMessage(responseBody) ??
    `Nona request failed with HTTP ${response.status} (${response.statusText}).`;
  throw new NonaClientError(
    message,
    response.status,
    method,
    url,
    responseBody,
  );
}

function readErrorMessage(responseBody: string): string | undefined {
  if (!responseBody.trim()) {
    return undefined;
  }

  try {
    const parsed = JSON.parse(responseBody) as {
      error?: unknown;
      message?: unknown;
    };
    if (typeof parsed.error === "string") {
      return parsed.error;
    }

    if (typeof parsed.message === "string") {
      return parsed.message;
    }
  } catch {
    return undefined;
  }

  return undefined;
}

function parseLegacyConfigValue(responseBody: string): NonaConfigValue {
  const parsed = JSON.parse(responseBody) as {
    value?: unknown;
    contentType?: unknown;
  };

  if (typeof parsed.value !== "string") {
    throw new Error("The response JSON must include a string 'value' property.");
  }

  if (typeof parsed.contentType !== "string") {
    throw new Error(
      "The response JSON must include a string 'contentType' property.",
    );
  }

  return {
    value: parsed.value,
    contentType: normalizeContentType(parsed.contentType),
  };
}

function normalizeContentType(contentType: string): string {
  switch (contentType.trim().toLowerCase()) {
    case "json":
    case "application/json":
    case "text/json":
      return "json";
    case "number":
    case "integer":
    case "float":
    case "double":
    case "decimal":
      return "number";
    case "boolean":
    case "bool":
      return "boolean";
    case "text":
    case "string":
    case "plain":
    case "text/plain":
      return "text";
    default:
      return contentType;
  }
}

function inferContentType(value: string): string {
  try {
    const parsed = JSON.parse(value) as unknown;

    if (typeof parsed === "boolean") {
      return "boolean";
    }

    if (typeof parsed === "number") {
      return "number";
    }

    if (parsed === null || Array.isArray(parsed) || typeof parsed === "object") {
      return "json";
    }
  } catch {
    return "text";
  }

  return "text";
}
