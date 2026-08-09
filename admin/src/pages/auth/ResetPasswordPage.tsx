import { Title } from "@solidjs/meta";
import { useNavigate, useParams } from "@solidjs/router";
import { useMutation, useQuery } from "@tanstack/solid-query";
import { createSignal, Show } from "solid-js";
import { authService } from "../../entities/auth/api/auth.service";
import { authStore } from "../../entities/auth/model/store";
import { getActiveProjectHref } from "../../entities/project/model/active-project";
import { ApiRequestError } from "../../shared/api/client";
import { MSG } from "../../shared/lib/messages";
import { Button } from "../../shared/ui/button";
import { MIcon } from "../../shared/ui/icons";
import { AuthCard } from "../../widgets/auth-shell/AuthCard";
import { FormField } from "../../widgets/auth-shell/FormField";
import { PasswordStrengthMeter } from "../../widgets/auth-shell/PasswordStrengthMeter";

export default function ResetPasswordPage() {
  const params = useParams<{ token: string }>();
  const navigate = useNavigate();
  const [password, setPassword] = createSignal("");
  const [confirmPassword, setConfirmPassword] = createSignal("");
  const [error, setError] = createSignal("");
  const [completedWhileAuthenticated, setCompletedWhileAuthenticated] = createSignal(false);

  const resetQuery = useQuery(() => ({
    queryKey: ["password-reset", params.token],
    queryFn: () => authService.getPasswordReset(params.token),
    retry: false
  }));

  const resetMutation = useMutation(() => ({
    mutationFn: (newPassword: string) =>
      authService.completePasswordReset(params.token, newPassword),
    onSuccess: () => {
      if (authService.isAuthenticated()) {
        setCompletedWhileAuthenticated(true);
        return;
      }

      navigate("/login?passwordReset=success", { replace: true });
    },
    onError: caught => setError(resetErrorMessage(caught))
  }));

  const handleSubmit = (event: Event) => {
    event.preventDefault();
    setError("");
    if (password() !== confirmPassword()) {
      setError(MSG.PASSWORD_MISMATCH);
      return;
    }

    resetMutation.mutate(password());
  };

  const loadError = () => resetErrorMessage(resetQuery.error);

  const signInWithNewPassword = () => {
    authStore.clearSession();
    navigate("/login?passwordReset=success", { replace: true });
  };

  const returnToConsole = () => navigate(getActiveProjectHref(), { replace: true });

  return (
    <>
      <Title>Reset Password | Nona Config Admin</Title>
      <Show
        when={!completedWhileAuthenticated()}
        fallback={
          <AuthCard
            title="Password Updated"
            description={MSG.PASSWORD_RESET_COMPLETE}
            testId="password-reset-complete"
          >
            <div class="space-y-3">
              <Button
                type="button"
                size="lg"
                class="w-full"
                data-testid="password-reset-sign-in"
                onClick={signInWithNewPassword}
              >
                <span>Sign in with new password</span>
                <MIcon name="login" class="text-[18px]" />
              </Button>
              <Button
                type="button"
                size="lg"
                variant="outline"
                class="w-full"
                data-testid="password-reset-return-console"
                onClick={returnToConsole}
              >
                <span>Return to console</span>
                <MIcon name="arrow_forward" class="text-[18px]" />
              </Button>
            </div>
          </AuthCard>
        }
      >
        <Show
          when={resetQuery.isSuccess}
          fallback={
            <AuthCard
              title={resetQuery.isPending ? "Validating Reset Link" : "Reset Link Unavailable"}
              description={
                resetQuery.isPending ? "Checking your password reset link…" : loadError()
              }
              error={!resetQuery.isPending ? loadError() : undefined}
            >
              <Show when={resetQuery.isPending}>
                <p class="text-on-surface-variant text-center text-sm">
                  Please wait while we verify your link.
                </p>
              </Show>
            </AuthCard>
          }
        >
          <AuthCard
            title="Set a New Password"
            description={`Create a new password for ${resetQuery.data?.email}.`}
            error={error()}
          >
            <form onSubmit={handleSubmit} class="space-y-5">
              <div>
                <FormField
                  id="reset-password"
                  label="New Password"
                  type="password"
                  placeholder="••••••••••••"
                  value={password()}
                  onInput={event => setPassword(event.currentTarget.value)}
                  required
                  autofocus
                  autocomplete="new-password"
                  leftIcon="key"
                />
                <PasswordStrengthMeter password={password()} />
              </div>
              <FormField
                id="reset-confirm-password"
                label="Confirm Password"
                type="password"
                placeholder="••••••••••••"
                value={confirmPassword()}
                onInput={event => setConfirmPassword(event.currentTarget.value)}
                required
                autocomplete="new-password"
                leftIcon="shield_lock"
              />
              <Button
                type="submit"
                size="lg"
                disabled={resetMutation.isPending}
                class="w-full"
              >
                <span>{resetMutation.isPending ? "Updating password…" : "Set New Password"}</span>
                <MIcon name="lock_reset" class="text-[18px]" />
              </Button>
            </form>
          </AuthCard>
        </Show>
      </Show>
    </>
  );
}

function resetErrorMessage(caught: unknown) {
  if (caught instanceof ApiRequestError && caught.code === "password_reset_invalid_or_used") {
    return "This password reset link is invalid, expired, or has already been used.";
  }

  if (caught instanceof Error && caught.message) {
    return caught.message;
  }

  return "We couldn't use this password reset link.";
}
