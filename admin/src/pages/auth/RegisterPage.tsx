import { A, useNavigate } from "@solidjs/router";
import { useMutation } from "@tanstack/solid-query";
import { createSignal } from "solid-js";
import { authService } from "../../entities/auth/api/auth.service";
import { authStore } from "../../entities/auth/model/store";
import { MSG } from "../../shared/lib/messages";
import type { RegisterRequest } from "../../types";
import { AuthCard } from "../../widgets/auth-shell/AuthCard";
import { FormField } from "../../widgets/auth-shell/FormField";
import { PasswordStrengthMeter } from "../../widgets/auth-shell/PasswordStrengthMeter";

export default function RegisterPage() {
  const navigate = useNavigate();
  const [email, setEmail] = createSignal("");
  const [password, setPassword] = createSignal("");
  const [confirmPassword, setConfirmPassword] = createSignal("");
  const [error, setError] = createSignal("");

  const registerMutation = useMutation(() => ({
    mutationFn: (data: RegisterRequest) => authService.register(data),
    onSuccess: result => {
      if (result.success && result.response?.token) {
        authStore.saveSession(result.response.token, {
          email: email(),
          role: result.response.role
        });
        navigate("/projects");
      } else if (result.error) {
        setError(result.error);
      } else {
        setError(MSG.REGISTER_FAILED);
      }
    },
    onError: () => {
      setError(MSG.REGISTER_UNEXPECTED);
    }
  }));

  const handleSubmit = (e: Event) => {
    e.preventDefault();
    setError("");
    if (password() !== confirmPassword()) {
      setError(MSG.PASSWORD_MISMATCH);
      return;
    }
    registerMutation.mutate({ email: email(), password: password() });
  };

  return (
    <>
      <AuthCard
        title="Admin Registration"
        description="Initialize the root administrator account"
        error={error()}
        testId="register-card"
        headingTestId="register-heading"
      >
        <form onSubmit={handleSubmit} class="space-y-5">
          <FormField
            id="reg-username"
            label="System Username"
            type="text"
            placeholder="admin@nona.dev"
            value={email()}
            onInput={e => setEmail(e.currentTarget.value)}
            required
            autofocus
            autocomplete="username"
            leftIcon="alternate_email"
            testId="register-email-input"
          />

          <div class="space-y-1.5">
            <FormField
              id="reg-password"
              label="Root Password"
              type="password"
              placeholder="••••••••••••"
              value={password()}
              onInput={e => setPassword(e.currentTarget.value)}
              required
              autocomplete="new-password"
              leftIcon="key"
              testId="register-password-input"
            />
            <PasswordStrengthMeter password={password()} />
          </div>

          <FormField
            id="reg-confirm-password"
            label="Verify Password"
            type="password"
            placeholder="••••••••••••"
            value={confirmPassword()}
            onInput={e => setConfirmPassword(e.currentTarget.value)}
            required
            autocomplete="new-password"
            leftIcon="shield_lock"
            testId="register-confirm-password-input"
          />

          <div class="pt-2">
            <button
              type="submit"
              data-testid="register-submit-button"
              disabled={registerMutation.isPending}
              class="bg-primary text-on-primary flex w-full cursor-pointer items-center justify-center gap-2 rounded-lg border-0 py-3.5 text-[13px] font-semibold transition-all hover:brightness-105 active:scale-[0.97] disabled:cursor-not-allowed disabled:opacity-50"
            >
              <span>{registerMutation.isPending ? "Creating account…" : "Create Account"}</span>
              <span class="material-symbols-outlined text-[18px]">arrow_right_alt</span>
            </button>
          </div>
        </form>

        <div class="text-on-surface-variant mt-5 text-center text-[12px]">
          Already have an account?{" "}
          <A
            href="/login"
            class="text-primary hover:text-primary/80 font-bold transition-colors hover:underline"
          >
            Log in
          </A>
        </div>

        {/* Security Info footer */}
        <div class="border-outline-variant/15 mt-6 border-t pt-5">
          <div class="text-outline flex items-center justify-center gap-6 text-[10px] font-medium">
            <span class="flex items-center gap-1.5">
              <span class="material-symbols-outlined text-[14px]">lock</span>
              TLS 1.3
            </span>
            <span class="flex items-center gap-1.5">
              <span class="material-symbols-outlined text-[14px]">history</span>
              Audited
            </span>
            <span class="flex items-center gap-1.5">
              <span class="material-symbols-outlined text-[14px]">verified</span>
              Local
            </span>
          </div>
        </div>
      </AuthCard>
    </>
  );
}
