import { PublicClientApplication } from "@azure/msal-browser";

function createMicrosoftApplication(clientId: string, authority: string) {
  return new PublicClientApplication({
    auth: {
      clientId,
      authority,
      redirectUri: getMicrosoftRedirectUri(),
      navigateToLoginRequestUrl: false,
    },
    cache: {
      cacheLocation: "sessionStorage",
      storeAuthStateInCookie: false,
    },
  });
}

export async function signInWithMicrosoftRedirect(
  clientId: string,
  authority: string,
  flowId: string,
) {
  const application = createMicrosoftApplication(clientId, authority);

  if (application.initialize) {
    await application.initialize();
  }

  try {
    await application.loginRedirect({
      scopes: ["openid", "profile", "email"],
      prompt: "select_account",
      redirectUri: getMicrosoftRedirectUri(),
      state: flowId,
    });
  } catch (error) {
    console.error("Microsoft sign-in redirect failed.", {
      error,
      authority,
      clientId,
    });

    throw new Error(mapMicrosoftError(error));
  }
}

export async function handleMicrosoftRedirect(clientId: string, authority: string) {
  const application = createMicrosoftApplication(clientId, authority);

  if (application.initialize) {
    await application.initialize();
  }

  try {
    const result = await application.handleRedirectPromise();
    if (!result?.idToken) {
      throw new Error("missing_id_token");
    }

    return result.idToken;
  } catch (error) {
    console.error("Microsoft sign-in callback failed.", {
      error,
      authority,
      clientId,
    });

    throw new Error(mapMicrosoftError(error));
  }
}

function getMicrosoftRedirectUri() {
  return new URL("/sso/callback/microsoft", window.location.origin).toString();
}

function mapMicrosoftError(error: unknown) {
  if (error instanceof Error) {
    const code = error.message.toLowerCase();

    if (code.includes("user_cancelled") || code.includes("access_denied")) {
      return "Microsoft sign-in was cancelled.";
    }

    if (code.includes("missing_id_token")) {
      return "Microsoft sign-in did not return a token.";
    }
  }

  return "Microsoft sign-in is unavailable right now. Please try again.";
}
