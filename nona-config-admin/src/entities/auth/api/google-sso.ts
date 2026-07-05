import type { SsoProvider } from "./sso-provider";

type GoogleCredentialResponse = {
  credential?: string;
};

type GooglePromptError = {
  type?: string;
};

type GoogleAccountsId = {
  initialize: (options: {
    client_id: string;
    callback: (response: GoogleCredentialResponse) => void;
    ux_mode?: "popup" | "redirect";
    cancel_on_tap_outside?: boolean;
    error_callback?: (error: GooglePromptError) => void;
  }) => void;
  renderButton: (element: HTMLElement, options: Record<string, unknown>) => void;
  cancel: () => void;
};

type GoogleRuntime = {
  accounts: {
    id: GoogleAccountsId;
  };
};

declare global {
  interface Window {
    google?: GoogleRuntime;
  }
}

let googleScriptPromise: Promise<GoogleRuntime> | null = null;

export async function renderGoogleSsoButton(
  container: HTMLElement,
  clientId: string,
  onCredential: (idToken: string) => void | Promise<void>,
  onError: (message: string) => void,
) {
  const google = await loadGoogleRuntime();

  container.replaceChildren();
  google.accounts.id.initialize({
    client_id: clientId,
    ux_mode: "popup",
    cancel_on_tap_outside: true,
    callback: (response) => {
      if (!response.credential) {
        onError("Google sign-in did not return a token.");
        return;
      }

      void onCredential(response.credential);
    },
    error_callback: (error) => {
      onError(mapGoogleError(error.type));
    },
  });

  google.accounts.id.renderButton(container, {
    theme: "outline",
    size: "large",
    text: "continue_with",
    shape: "rectangular",
    width: 320,
  });

  return () => {
    google.accounts.id.cancel();
    container.replaceChildren();
  };
}

async function loadGoogleRuntime(): Promise<GoogleRuntime> {
  if (window.google?.accounts?.id) {
    return window.google;
  }

  if (!googleScriptPromise) {
    googleScriptPromise = new Promise<GoogleRuntime>((resolve, reject) => {
      const existing = document.querySelector<HTMLScriptElement>('script[data-google-sso="true"]');
      if (existing) {
        existing.addEventListener("load", () => {
          if (window.google?.accounts?.id) {
            resolve(window.google);
            return;
          }

          reject(new Error("Google Identity Services loaded without runtime."));
        });
        existing.addEventListener("error", () => reject(new Error("Failed to load Google Identity Services.")));
        return;
      }

      const script = document.createElement("script");
      script.src = "https://accounts.google.com/gsi/client";
      script.async = true;
      script.defer = true;
      script.dataset.googleSso = "true";
      script.onload = () => {
        if (window.google?.accounts?.id) {
          resolve(window.google);
          return;
        }

        reject(new Error("Google Identity Services loaded without runtime."));
      };
      script.onerror = () => reject(new Error("Failed to load Google Identity Services."));
      document.head.appendChild(script);
    });
  }

  return googleScriptPromise;
}

function mapGoogleError(type?: string) {
  switch (type) {
    case "popup_failed_to_open":
      return "Google sign-in popup was blocked. Please allow popups and try again.";
    case "popup_closed":
      return "Google sign-in was cancelled.";
    default:
      return "Google sign-in is unavailable right now. Please try again.";
  }
}

/**
 * Creates a {@link SsoProvider} that signs in via a Google one-tap button rendered into `container`.
 * The returned `signIn()` waits until the user completes the Google flow.
 */
export function createGoogleSsoProvider(container: HTMLElement, clientId: string): SsoProvider {
  return {
    signIn: () =>
      new Promise<string>((resolve, reject) => {
        void renderGoogleSsoButton(container, clientId, resolve, (msg) => reject(new Error(msg)));
      }),
  };
}
