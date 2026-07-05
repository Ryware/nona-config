import { PublicClientApplication } from "@azure/msal-browser";
import type { SsoProvider } from "./sso-provider";

export async function signInWithMicrosoftPopup(clientId: string, authority: string) {
  const application = new PublicClientApplication({
    auth: {
      clientId,
      authority,
      redirectUri: window.location.origin,
      navigateToLoginRequestUrl: false,
    },
    cache: {
      cacheLocation: "sessionStorage",
      storeAuthStateInCookie: false,
    },
  });

  if (application.initialize) {
    await application.initialize();
  }

  try {
    const result = await application.loginPopup({
      scopes: ["openid", "profile", "email"],
      prompt: "select_account",
      redirectUri: window.location.origin,
    });

    if (!result.idToken) {
      throw new Error("missing_id_token");
    }

    return result.idToken;
  } catch (error) {
    console.error("Microsoft sign-in failed.", {
      error,
      authority,
      clientId,
    });

    throw new Error(mapMicrosoftError(error));
  }
}

function mapMicrosoftError(error: unknown) {
  if (error instanceof Error) {
    const code = error.message.toLowerCase();

    if (code.includes("user_cancelled")) {
      return "Microsoft sign-in was cancelled.";
    }

    if (code.includes("popup_window_error") || code.includes("popup_window")) {
      return "Microsoft sign-in popup was blocked. Please allow popups and try again.";
    }

    if (code.includes("missing_id_token")) {
      return "Microsoft sign-in did not return a token.";
    }
  }

  return "Microsoft sign-in is unavailable right now. Please try again.";
}

/**
 * Creates a {@link SsoProvider} that signs in via a Microsoft popup.
 */
export function createMicrosoftSsoProvider(clientId: string, authority: string): SsoProvider {
  return {
    signIn: () => signInWithMicrosoftPopup(clientId, authority),
  };
}
