import { Title } from "@solidjs/meta";
import { useMutation, useQuery } from "@tanstack/solid-query";
import { createSignal, Show } from "solid-js";
import { authService } from "../../entities/auth/api/auth.service";
import { ApiRequestError } from "../../shared/api/client";
import { MSG } from "../../shared/lib/messages";
import { validateNewPassword } from "../../shared/lib/password-policy";
import { Button } from "../../shared/ui/button";
import { MIcon } from "../../shared/ui/icons";
import { QueryErrorBanner } from "../../shared/ui/QueryGuard";
import { useToast } from "../../shared/ui/toast";
import { FormField } from "../../widgets/auth-shell/FormField";
import { PasswordStrengthMeter } from "../../widgets/auth-shell/PasswordStrengthMeter";

export default function AccountPage() {
  const { addToast } = useToast();
  const [currentPassword, setCurrentPassword] = createSignal("");
  const [newPassword, setNewPassword] = createSignal("");
  const [confirmPassword, setConfirmPassword] = createSignal("");
  const [error, setError] = createSignal("");

  const accountQuery = useQuery(() => ({
    queryKey: ["current-account"],
    queryFn: () => authService.getCurrentAccount(),
    retry: false
  }));

  const changeMutation = useMutation(() => ({
    mutationFn: () =>
      authService.changePassword({
        currentPassword: currentPassword(),
        newPassword: newPassword()
      }),
    onSuccess: () => {
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
      setError("");
      addToast(MSG.PASSWORD_CHANGED, "success");
    },
    onError: caught => setError(changePasswordError(caught))
  }));

  const handleSubmit = (event: Event) => {
    event.preventDefault();
    setError("");
    const passwordError = validateNewPassword(newPassword(), confirmPassword());
    if (passwordError) {
      setError(passwordError);
      return;
    }
    if (newPassword() === currentPassword()) {
      setError("New password must be different from the current password.");
      return;
    }

    changeMutation.mutate();
  };

  return (
    <>
      <Title>Account | Nona Config Admin</Title>
      <div class="mx-auto max-w-3xl space-y-6">
        <section class="bg-surface-container-low border-outline-variant/15 rounded-2xl border p-5 md:p-6">
          <div class="flex items-start gap-4">
            <div class="bg-primary/10 text-primary border-primary/20 flex h-11 w-11 shrink-0 items-center justify-center rounded-xl border">
              <MIcon name="manage_accounts" class="text-[24px]" />
            </div>
            <div>
              <h1 class="font-headline text-on-surface text-lg font-bold">Account</h1>
              <p class="text-on-surface-variant mt-1 text-sm">
                Manage your sign-in credentials and account security.
              </p>
            </div>
          </div>
        </section>

        <Show when={accountQuery.isError}>
          <QueryErrorBanner
            message="Failed to load account details."
            onRetry={() => accountQuery.refetch()}
          />
        </Show>

        <Show when={accountQuery.isLoading}>
          <div class="skeleton h-80 w-full rounded-2xl" />
        </Show>

        <Show when={accountQuery.isSuccess && accountQuery.data}>
          <section class="bg-surface-container-low border-outline-variant/15 rounded-2xl border p-5 md:p-6">
            <div class="border-outline-variant/15 mb-6 border-b pb-5">
              <p class="font-headline text-on-surface font-semibold">
                {accountQuery.data!.name || accountQuery.data!.email}
              </p>
              <p class="text-outline mt-1 font-mono text-xs">{accountQuery.data!.email}</p>
              <span class="bg-primary/10 text-primary mt-3 inline-flex rounded-md px-2 py-0.5 text-[11px] font-medium capitalize">
                {accountQuery.data!.role}
              </span>
            </div>

            <Show
              when={accountQuery.data!.passwordEnabled}
              fallback={
                <div data-testid="password-managed-by-sso" class="flex items-start gap-3 py-2">
                  <MIcon name="verified_user" class="text-primary text-[22px]" />
                  <div>
                    <h2 class="font-headline text-on-surface text-sm font-semibold">
                      Password managed by SSO
                    </h2>
                    <p class="text-on-surface-variant mt-1 text-sm">
                      This account signs in through an external identity provider and does not have
                      a Nona Config password.
                    </p>
                  </div>
                </div>
              }
            >
              <div>
                <h2 class="font-headline text-on-surface text-sm font-semibold">Change Password</h2>
                <p class="text-on-surface-variant mt-1 text-xs">
                  Enter your current password before choosing a new one.
                </p>
              </div>

              <Show when={error()}>
                <div
                  role="alert"
                  class="bg-error/8 text-error border-error/15 mt-5 rounded-xl border px-3.5 py-3 text-xs"
                >
                  {error()}
                </div>
              </Show>

              <form onSubmit={handleSubmit} class="mt-5 space-y-5">
                <FormField
                  id="current-password"
                  label="Current Password"
                  type="password"
                  placeholder="••••••••••••"
                  value={currentPassword()}
                  onInput={event => setCurrentPassword(event.currentTarget.value)}
                  required
                  autocomplete="current-password"
                  leftIcon="lock"
                />
                <div>
                  <FormField
                    id="new-password"
                    label="New Password"
                    type="password"
                    placeholder="••••••••••••"
                    value={newPassword()}
                    onInput={event => setNewPassword(event.currentTarget.value)}
                    required
                    autocomplete="new-password"
                    leftIcon="key"
                  />
                  <PasswordStrengthMeter password={newPassword()} />
                </div>
                <FormField
                  id="confirm-new-password"
                  label="Confirm New Password"
                  type="password"
                  placeholder="••••••••••••"
                  value={confirmPassword()}
                  onInput={event => setConfirmPassword(event.currentTarget.value)}
                  required
                  autocomplete="new-password"
                  leftIcon="shield_lock"
                />
                <div class="flex justify-end">
                  <Button type="submit" disabled={changeMutation.isPending}>
                    <MIcon name="lock_reset" class="text-[17px]" />
                    {changeMutation.isPending ? "Changing password…" : "Change Password"}
                  </Button>
                </div>
              </form>
            </Show>
          </section>
        </Show>
      </div>
    </>
  );
}

function changePasswordError(caught: unknown) {
  if (caught instanceof ApiRequestError) {
    if (caught.code === "current_password_invalid") return "Current password is incorrect.";
    if (caught.code === "new_password_must_differ") {
      return "New password must be different from the current password.";
    }
    if (caught.code === "password_change_unavailable") {
      return "Password change is not available for this account.";
    }
  }

  if (caught instanceof Error && caught.message) return caught.message;
  return MSG.PASSWORD_CHANGE_FAILED;
}
