import { createEffect, createSignal, onCleanup, onMount, Show } from "solid-js";
import type { SsoConfig } from "../../types";
import { renderGoogleSsoButton } from "../../entities/auth/api/google-sso";
import { signInWithMicrosoftRedirect } from "../../entities/auth/api/microsoft-sso";
import {
  beginSsoRedirect,
  cancelSsoRedirect,
  consumeSsoRedirectResult,
  createSsoFlowId,
} from "../../entities/auth/api/sso-redirect";
import { Button } from "../../shared/ui/button";

interface SsoSectionProps {
  ssoConfig?: SsoConfig;
  isBusy: boolean;
  onSsoSuccess: (provider: "google" | "microsoft", idToken: string) => Promise<void>;
  onSsoError: (message: string) => void;
  onRedirectStart?: () => void;
  dividerLabel?: string;
}

export function SsoSection(props: SsoSectionProps) {
  const [activeSsoProvider, setActiveSsoProvider] = createSignal<"google" | "microsoft" | null>(null);
  let googleButtonHost: HTMLDivElement | undefined;

  const hasSsoOptions = () => !!(props.ssoConfig?.google.enabled || props.ssoConfig?.microsoft.enabled);
  const isCurrentlyBusy = () => props.isBusy || activeSsoProvider() !== null;

  onMount(() => {
    const result = consumeSsoRedirectResult();
    if (!result) {
      return;
    }

    if (result.error || !result.idToken) {
      props.onSsoError(result.error || "SSO sign-in did not return a token.");
      return;
    }

    setActiveSsoProvider(result.provider);
    props.onSsoError("");

    void props.onSsoSuccess(result.provider, result.idToken)
      .catch(() => {
        // The parent maps backend errors to the authentication page.
      })
      .finally(() => {
        setActiveSsoProvider(null);
      });
  });

	createEffect(() => {
	  const googleConfig = props.ssoConfig?.google;
	  const onSsoError = props.onSsoError;
	  const onRedirectStart = props.onRedirectStart;
	  if (!googleConfig?.enabled || !googleConfig.clientId || !googleButtonHost) {
	    return;
	  }

    let disposed = false;
    let cleanup: (() => void) | undefined;
    const flowId = createSsoFlowId();

    void renderGoogleSsoButton(
      googleButtonHost,
      googleConfig.clientId,
      flowId,
      () => {
        try {
          onRedirectStart?.();
          beginSsoRedirect("google", flowId);
          setActiveSsoProvider("google");
          onSsoError("");
        } catch (caught) {
          onSsoError(
            caught instanceof Error
              ? caught.message
              : "Google sign-in is unavailable right now. Please try again.",
          );
        }
      },
      (message) => {
	        onSsoError(message);
        setActiveSsoProvider(null);
      },
    )
      .then((nextCleanup) => {
        if (disposed) {
          nextCleanup();
          return;
        }
        cleanup = nextCleanup;
      })
      .catch(() => {
	        onSsoError("Google sign-in is unavailable right now. Please try again.");
      });

    onCleanup(() => {
      disposed = true;
      cleanup?.();
    });
  });

  const handleMicrosoftLogin = async () => {
    const microsoftConfig = props.ssoConfig?.microsoft;
    if (!microsoftConfig?.enabled || !microsoftConfig.clientId || !microsoftConfig.authority) {
      props.onSsoError("Microsoft sign-in is not configured.");
      return;
    }

    setActiveSsoProvider("microsoft");
    props.onSsoError("");
    let flowId: string | undefined;

    try {
      props.onRedirectStart?.();
      flowId = beginSsoRedirect("microsoft");
      await signInWithMicrosoftRedirect(
        microsoftConfig.clientId,
        microsoftConfig.authority,
        flowId,
      );
    } catch (caught) {
      if (flowId) {
        cancelSsoRedirect("microsoft", flowId);
      }
      props.onSsoError(
        caught instanceof Error && caught.message
          ? caught.message
          : "Microsoft sign-in failed. Please try again."
      );
    } finally {
      setActiveSsoProvider(null);
    }
  };

  return (
    <Show when={hasSsoOptions()}>
      <div class="my-6 flex items-center gap-4 text-outline text-[9px] font-bold uppercase tracking-widest">
        <div class="h-px flex-1 bg-outline-variant/30" />
        <span>{props.dividerLabel || "Or continue with SSO"}</span>
        <div class="h-px flex-1 bg-outline-variant/30" />
      </div>

      <div class="space-y-3">
        <Show when={props.ssoConfig?.google.enabled}>
          <div class="flex flex-col gap-2">
            <div ref={(element) => { googleButtonHost = element; }} class="w-full flex justify-center" />
            <Show when={activeSsoProvider() === "google"}>
              <p class="text-center text-[10px] text-outline uppercase tracking-wider font-bold">Redirecting to Google…</p>
            </Show>
          </div>
        </Show>

        <Show when={props.ssoConfig?.microsoft.enabled}>
          <Button
            variant="outline"
            class="w-full h-11 text-on-surface text-xs font-bold uppercase tracking-wider bg-surface-container-low border border-outline-variant/15 hover:bg-surface-container-high/40 transition-colors flex items-center justify-center gap-2"
            disabled={isCurrentlyBusy()}
            onClick={handleMicrosoftLogin}
          >
            <span class="material-symbols-outlined text-[18px]">domain_verification</span>
            {activeSsoProvider() === "microsoft" ? "Redirecting to Microsoft…" : "Continue with Microsoft"}
          </Button>
        </Show>
      </div>
    </Show>
  );
}
