import { useNavigate, useParams } from "@solidjs/router";
import { useMutation, useQuery } from "@tanstack/solid-query";
import { createSignal, Show } from "solid-js";
import { authService } from "../../entities/auth/api/auth.service";
import { authStore } from "../../entities/auth/model/store";
import { ApiRequestError } from "../../shared/api/client";
import { Button } from "../../shared/ui/button";
import { MIcon } from "../../shared/ui/icons";
import type { LoginResponse } from "../../types";
import { AuthCard } from "../../widgets/auth-shell/AuthCard";
import { FormField } from "../../widgets/auth-shell/FormField";
import { SsoSection } from "../../widgets/auth-shell/SsoSection";

export default function InvitePage() {
  const navigate = useNavigate();
  const params = useParams<{ token: string }>();
  const [password, setPassword] = createSignal("");
  const [confirmPassword, setConfirmPassword] = createSignal("");
  const [error, setError] = createSignal("");

  const completeLogin = (result: LoginResponse) => {
    authStore.saveSession(result.token, { email: result.username ?? "", role: result.role });
    navigate("/projects");
  };

  const invitationQuery = useQuery(() => ({
    queryKey: ["invitation", params.token],
    queryFn: () => authService.getInvitation(params.token),
    retry: false
  }));

  const ssoConfigQuery = useQuery(() => ({
    queryKey: ["sso-config"],
    queryFn: () => authService.getSsoConfig(),
    retry: false,
    enabled: invitationQuery.isSuccess
  }));

  const passwordMutation = useMutation(() => ({
    mutationFn: (newPassword: string) =>
      authService.completeInvitationWithPassword(params.token, newPassword),
    onSuccess: completeLogin,
    onError: caught => {
      setError(getInviteErrorMessage(caught, "Unable to complete invitation right now."));
    }
  }));

  const handleSubmit = (e: Event) => {
    e.preventDefault();
    setError("");

    if (password() !== confirmPassword()) {
      setError("Passwords do not match.");
      return;
    }

    passwordMutation.mutate(password());
  };

  const handleSsoSuccess = async (provider: "google" | "microsoft", idToken: string) => {
    try {
      const result =
        provider === "google"
          ? await authService.completeInvitationWithGoogle(params.token, idToken)
          : await authService.completeInvitationWithMicrosoft(params.token, idToken);
      completeLogin(result);
    } catch (caught) {
      setError(
        getInviteErrorMessage(
          caught,
          `${provider === "google" ? "Google" : "Microsoft"} sign-in failed. Please try again.`
        )
      );
      throw caught;
    }
  };

  const isBusy = () => passwordMutation.isPending;
  const inviteLoadError = () => getInviteLoadError(invitationQuery.error);

  return (
    <>
      <Show
        when={invitationQuery.isSuccess}
        fallback={
          <AuthCard
            title={invitationQuery.isPending ? "Validating Invitation" : "Invitation Unavailable"}
            description={
              invitationQuery.isPending ? "Checking your invitation link…" : inviteLoadError()
            }
            error={!invitationQuery.isPending ? inviteLoadError() : undefined}
          >
            <Show when={invitationQuery.isPending}>
              <p class="text-on-surface-variant text-center text-sm">
                Please wait while we verify your link.
              </p>
            </Show>
          </AuthCard>
        }
      >
        <AuthCard
          title="Complete Your Invitation"
          description={`You're joining as ${invitationQuery.data?.email}. Create a password or continue with SSO to activate your account.`}
          error={error()}
        >
          <div class="border-outline-variant/15 bg-surface-container-low/40 text-on-surface mb-6 rounded-xl border px-4 py-3.5 text-xs">
            <div class="text-outline font-headline mb-1.5 text-[9px] font-bold tracking-wider uppercase">
              Invited Account
            </div>
            <div class="text-on-surface text-sm font-bold">
              {invitationQuery.data?.name || invitationQuery.data?.email}
            </div>
            <div class="text-on-surface-variant mt-0.5 font-mono text-[11px]">
              {invitationQuery.data?.email}
            </div>
          </div>

          <form onSubmit={handleSubmit} class="space-y-5">
            <FormField
              id="password"
              label="Create Password"
              type="password"
              placeholder="••••••••••••"
              value={password()}
              onInput={e => setPassword(e.currentTarget.value)}
              required
              autofocus
              autocomplete="new-password"
              leftIcon="key"
            />
            <FormField
              id="confirm-password"
              label="Confirm Password"
              type="password"
              placeholder="••••••••••••"
              value={confirmPassword()}
              onInput={e => setConfirmPassword(e.currentTarget.value)}
              required
              autocomplete="new-password"
              leftIcon="shield_lock"
            />
            <div class="pt-2">
              <Button
                type="submit"
                size="lg"
                disabled={isBusy()}
                class="w-full"
              >
                <span>
                  {passwordMutation.isPending ? "Activating account…" : "Set Password and Continue"}
                </span>
                <MIcon name="lock" class="text-[18px]" />
              </Button>
            </div>
          </form>

          <SsoSection
            ssoConfig={ssoConfigQuery.data}
            isBusy={isBusy()}
            onSsoSuccess={handleSsoSuccess}
            onSsoError={msg => setError(msg)}
            dividerLabel="Or skip password and use SSO"
          />
        </AuthCard>
      </Show>
    </>
  );
}

function getInviteLoadError(caught: unknown) {
  if (caught instanceof ApiRequestError && caught.code === "invitation_invalid_or_used") {
    return "This invitation link is invalid or has already been used.";
  }

  if (caught instanceof Error && caught.message) {
    return caught.message;
  }

  return "We couldn't validate this invitation link.";
}

function getInviteErrorMessage(caught: unknown, fallback: string) {
  if (caught instanceof ApiRequestError) {
    if (caught.code === "invitation_invalid_or_used") {
      return "This invitation link is invalid or has already been used.";
    }

    if (caught.code === "invitation_sso_email_mismatch") {
      return "The SSO email does not match the invited account email.";
    }

    if (caught.code === "sso_user_not_registered") {
      return "This SSO account is not registered for the invited email.";
    }
  }

  if (caught instanceof Error && caught.message) {
    return caught.message;
  }

  return fallback;
}
