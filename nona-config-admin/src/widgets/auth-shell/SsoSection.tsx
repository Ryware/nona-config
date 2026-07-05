import { createEffect, createSignal, onCleanup, Show } from "solid-js";
import type { SsoConfig } from "../../types";
import { renderGoogleSsoButton } from "../../entities/auth/api/google-sso";
import { signInWithMicrosoftPopup } from "../../entities/auth/api/microsoft-sso";
import { Button } from "../../shared/ui/button";

interface SsoSectionProps {
  ssoConfig?: SsoConfig;
  isBusy: boolean;
  onSsoSuccess: (provider: "google" | "microsoft", idToken: string) => Promise<void>;
  onSsoError: (message: string) => void;
  dividerLabel?: string;
}

export function SsoSection(props: SsoSectionProps) {
  const [activeSsoProvider, setActiveSsoProvider] = createSignal<"google" | "microsoft" | null>(null);
  let googleButtonHost: HTMLDivElement | undefined;

  const hasSsoOptions = () => !!(props.ssoConfig?.google.enabled || props.ssoConfig?.microsoft.enabled);
  const isCurrentlyBusy = () => props.isBusy || activeSsoProvider() !== null;

	createEffect(() => {
	  const googleConfig = props.ssoConfig?.google;
	  const onSsoError = props.onSsoError;
	  const onSsoSuccess = props.onSsoSuccess;
	  if (!googleConfig?.enabled || !googleConfig.clientId || !googleButtonHost) {
	    return;
	  }

    let disposed = false;
    let cleanup: (() => void) | undefined;

    void renderGoogleSsoButton(
      googleButtonHost,
      googleConfig.clientId,
      async (idToken) => {
	        setActiveSsoProvider("google");
	        onSsoError("");
	
	        try {
	          await onSsoSuccess("google", idToken);
        } catch {
          // Error is handled by parent, fallback reset is managed in finally
        } finally {
          setActiveSsoProvider(null);
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

    try {
      const idToken = await signInWithMicrosoftPopup(microsoftConfig.clientId, microsoftConfig.authority);
      await props.onSsoSuccess("microsoft", idToken);
    } catch (caught) {
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
              <p class="text-center text-[10px] text-outline uppercase tracking-wider font-bold">Verifying Google sign-in…</p>
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
            {activeSsoProvider() === "microsoft" ? "Connecting to Microsoft…" : "Continue with Microsoft"}
          </Button>
        </Show>
      </div>
    </Show>
  );
}
