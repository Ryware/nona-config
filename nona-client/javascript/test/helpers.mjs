import assert from "node:assert/strict";
import { NonaClientError } from "../dist/index.js";

if (typeof globalThis.Headers === "undefined") {
  globalThis.Headers = class HeadersShim {
    #values = new Map();

    constructor(init = undefined) {
      if (!init) {
        return;
      }

      if (Array.isArray(init)) {
        for (const [key, value] of init) {
          this.set(key, value);
        }

        return;
      }

      if (typeof init.forEach === "function") {
        init.forEach((value, key) => {
          this.set(key, value);
        });
        return;
      }

      for (const [key, value] of Object.entries(init)) {
        this.set(key, value);
      }
    }

    set(key, value) {
      this.#values.set(String(key).toLowerCase(), String(value));
    }

    get(key) {
      return this.#values.get(String(key).toLowerCase()) ?? null;
    }

    forEach(callback) {
      for (const [key, value] of this.#values.entries()) {
        callback(value, key, this);
      }
    }
  };
}

if (typeof globalThis.Response === "undefined") {
  globalThis.Response = class ResponseShim {
    constructor(body, init = {}) {
      this._body = body ?? "";
      this.status = init.status ?? 200;
      this.statusText = init.statusText ?? "";
      this.headers = new Headers(init.headers);
      this.ok = this.status >= 200 && this.status < 300;
      this.url = init.url ?? "https://nona.test";
    }

    async text() {
      return typeof this._body === "string" ? this._body : String(this._body);
    }
  };
}

export function jsonResponse(body, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      "Content-Type": "application/json"
    }
  });
}

export function configValueResponse(value, contentType = "text", status = 200) {
  return new Response(value, {
    status,
    headers: {
      "Content-Type": "application/json",
      "X-Nona-Content-Type": contentType
    }
  });
}

export function capture(url, init) {
  return {
    url,
    method: init?.method,
    headers: new Headers(init?.headers),
    body: init?.body
  };
}

export function deferred() {
  let resolve;
  let reject;
  const promise = new Promise((res, rej) => {
    resolve = res;
    reject = rej;
  });

  return { promise, resolve, reject };
}

export function wait(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export function isNonaError(status, message) {
  return (error) => {
    assert.ok(error instanceof NonaClientError);
    assert.equal(error.status, status);
    assert.equal(error.message, message);
    return true;
  };
}
