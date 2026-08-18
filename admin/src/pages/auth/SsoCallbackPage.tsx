import { useNavigate, useParams, useSearchParams } from "@solidjs/router";
import { onMount, Show } from "solid-js";
import { createSignal } from "solid-js";
import { authService } from "../../entities/auth/api/auth.service";
import { consumeGoogleRedirectCredential } from "../../entities/auth/api/google-sso";
import { handleMicrosoftRedirect } from "../../entities/auth/api/microsoft-sso";
import {
  completeSsoRedirect,
  getPendingSsoFlow,
  isValidFlowId,
  type SsoProviderName,
} from "../../entities/auth/api/sso-redirect";
import { AuthCard } from "../../widgets/auth-shell/AuthCard";
import { AuthLayout } from "../../widgets/auth-shell/AuthLayout";

export default function SsoCallbackPage() {
  const navigate = useNavigate();
  const params = useParams<{ provider: string }>();
  const [searchParams] = useSearchParams();
  const [terminalError, setTerminalError] = createSignal("");

  onMount(() => {
    void finishRedirect();
  });

  const finishRedirect = async () => {
    const provider = parseProvider(params.provider);
    if (!provider) {
      setTerminalError("This SSO provider is not supported.");
      return;
    }

    const flowId =
      provider === "google"
        ? firstSearchParam(searchParams.flow)
        : getPendingSsoFlow("microsoft");

    if (!isValidFlowId(flowId)) {
      setTerminalError("This SSO sign-in request is missing or has expired.");
      return;
    }

    try {
      const googleError = firstSearchParam(searchParams.error);
      if (provider === "google" && googleError) {
        throw new Error(mapGoogleCallbackError(googleError));
      }

      const idToken =
        provider === "google"
          ? await consumeGoogleRedirectCredential(flowId)
          : await completeMicrosoftRedirect();

      returnToSignInPage(provider, flowId, { idToken });
    } catch (caught) {
      returnToSignInPage(provider, flowId, {
        error:
          caught instanceof Error && caught.message
            ? caught.message
            : "SSO sign-in could not be completed. Please try again.",
      });
    }
  };

  const completeMicrosoftRedirect = async () => {
    const config = await authService.getSsoConfig();
    const microsoft = config.microsoft;
    if (!microsoft.enabled || !microsoft.clientId || !microsoft.authority) {
      throw new Error("Microsoft sign-in is not configured.");
    }

    return handleMicrosoftRedirect(microsoft.clientId, microsoft.authority);
  };

  const returnToSignInPage = (
    provider: SsoProviderName,
    flowId: string,
    result: { idToken?: string; error?: string },
  ) => {
    const returnPath = completeSsoRedirect(provider, flowId, result);
    if (!returnPath) {
      setTerminalError("This SSO sign-in request is missing or has expired.");
      return;
    }

    navigate(returnPath, { replace: true });
  };

  return (
    <AuthLayout>
      <AuthCard
        title={terminalError() ? "SSO Sign-in Unavailable" : "Completing Sign-in"}
        error={terminalError() || undefined}
      >
        <Show
          when={!terminalError()}
          fallback={
            <a
              href="/login"
              class="text-primary block text-center text-xs font-medium hover:underline"
            >
              Return to login
            </a>
          }
        >
          <p class="text-on-surface-variant text-center text-[12.5px]">
            Please wait while we return you to Nona Config.
          </p>
        </Show>
      </AuthCard>
    </AuthLayout>
  );
}

function parseProvider(value: string): SsoProviderName | null {
  return value === "google" || value === "microsoft" ? value : null;
}

function mapGoogleCallbackError(error: string) {
  switch (error) {
    case "csrf":
      return "Google sign-in could not be verified. Please try again.";
    case "missing_credential":
      return "Google sign-in did not return a token.";
    default:
      return "Google sign-in could not be completed. Please try again.";
  }
}

function firstSearchParam(value: string | string[] | null | undefined) {
  return Array.isArray(value) ? value[0] : value;
}
