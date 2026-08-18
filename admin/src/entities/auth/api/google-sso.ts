type GooglePromptError = {
  type?: string;
};

type GoogleAccountsId = {
  initialize: (options: {
    client_id: string;
    callback?: (response: { credential?: string }) => void;
    login_uri?: string;
    ux_mode?: "popup" | "redirect";
    cancel_on_tap_outside?: boolean;
    error_callback?: (error: GooglePromptError) => void;
  }) => void;
  renderButton: (element: HTMLElement, options: {
    theme: string;
    size: string;
    text: string;
    shape: string;
    width: number;
    state: string;
    click_listener: () => void;
  }) => void;
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
  flowId: string,
  onRedirectStart: () => void,
  onError: (message: string) => void,
) {
  const google = await loadGoogleRuntime();

  container.replaceChildren();
  google.accounts.id.initialize({
    client_id: clientId,
    ux_mode: "redirect",
    login_uri: new URL("/auth/sso/google/callback", window.location.origin).toString(),
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
    state: flowId,
    click_listener: onRedirectStart,
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
    default:
      return "Google sign-in is unavailable right now. Please try again.";
  }
}

export async function consumeGoogleRedirectCredential(flowId: string) {
  const response = await fetch(
    `/auth/sso/google/credential?flow=${encodeURIComponent(flowId)}`,
    {
      credentials: "same-origin",
      headers: { Accept: "application/json" },
    },
  );

  if (!response.ok) {
    throw new Error("Google sign-in could not be completed. Please try again.");
  }

  const body = await response.json() as { idToken?: string };
  if (!body.idToken) {
    throw new Error("Google sign-in did not return a token.");
  }

  return body.idToken;
}
