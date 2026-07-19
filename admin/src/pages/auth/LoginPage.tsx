import { A, type RouteSectionProps, useNavigate } from "@solidjs/router";
import { useMutation, useQuery } from "@tanstack/solid-query";
import { createEffect, createSignal, Show } from "solid-js";
import { authService } from "../../entities/auth/api/auth.service";
import { authStore } from "../../entities/auth/model/store";
import { ApiRequestError } from "../../shared/api/client";
import { MSG } from "../../shared/lib/messages";
import { Button } from "../../shared/ui/button";
import type { LoginRequest, LoginResponse } from "../../types";
import { AuthCard } from "../../widgets/auth-shell/AuthCard";
import { FormField } from "../../widgets/auth-shell/FormField";
import { SsoSection } from "../../widgets/auth-shell/SsoSection";

interface LoginPageProps extends Partial<RouteSectionProps> {
  onLoginSuccess?: (result: LoginResponse) => void;
}

export default function LoginPage(props: LoginPageProps = {}) {
  const navigate = useNavigate();
  const [email, setEmail] = createSignal("");
  const [password, setPassword] = createSignal("");
  const [error, setError] = createSignal("");
  const [rememberMe, setRememberMe] = createSignal(true);
  const [showForgot, setShowForgot] = createSignal(false);
  const [forgotEmail, setForgotEmail] = createSignal("");
  const [forgotSent, setForgotSent] = createSignal(false);

  const completeLogin = (result: LoginResponse) => {
    authStore.saveSession(
      result.token,
      { email: result.username ?? "", role: result.role },
      rememberMe()
    );
    if (props.onLoginSuccess) {
      props.onLoginSuccess(result);
      return;
    }

    navigate("/projects");
  };

  const loginMutation = useMutation(() => ({
    mutationFn: (credentials: LoginRequest) => authService.login(credentials),
    onSuccess: completeLogin,
    onError: () => {
      setError(MSG.LOGIN_FAILED);
    }
  }));

  const forgotMutation = useMutation(() => ({
    mutationFn: (e: string) => authService.requestPasswordReset({ email: e }),
    onSuccess: () => setForgotSent(true),
    onError: () => setError(MSG.FORGOT_SEND_FAILED)
  }));

  const firstTimeQuery = useQuery(() => ({
    queryKey: ["first-time"],
    queryFn: () => authService.firstTime()
  }));

  const ssoConfigQuery = useQuery(() => ({
    queryKey: ["sso-config"],
    queryFn: () => authService.getSsoConfig(),
    retry: false
  }));

  createEffect(() => {
    if (firstTimeQuery.isSuccess && firstTimeQuery.data === true) {
      navigate("/register");
    }
  });

  const handleSubmit = (e: Event) => {
    e.preventDefault();
    setError("");
    loginMutation.mutate({ email: email(), password: password() });
  };

  const handleForgotSubmit = (e: Event) => {
    e.preventDefault();
    setError("");
    forgotMutation.mutate(forgotEmail());
  };

  const handleSsoSuccess = async (provider: "google" | "microsoft", idToken: string) => {
    try {
      const result =
        provider === "google"
          ? await authService.loginWithGoogle(idToken)
          : await authService.loginWithMicrosoft(idToken);
      completeLogin(result);
    } catch (caught) {
      setError(
        getErrorMessage(
          caught,
          provider === "google" ? MSG.SSO_FAILED_GOOGLE : MSG.SSO_FAILED_MICROSOFT
        )
      );
      throw caught;
    }
  };

  const isBusy = () => loginMutation.isPending;

  return (
    <>
      <Show
        when={!showForgot()}
        fallback={
          <AuthCard
            title="Reset Password"
            error={error()}
            testId="forgot-card"
            headingTestId="forgot-heading"
          >
            <Show
              when={!forgotSent()}
              fallback={
                <div data-testid="forgot-sent-state" class="space-y-4 py-2 text-center">
                  <span class="material-symbols-outlined text-primary text-[40px]">
                    mark_email_read
                  </span>
                  <p class="text-on-surface-variant text-[13px]">
                    If an account exists for{" "}
                    <strong class="text-on-surface">{forgotEmail()}</strong>, a reset link has been
                    sent.
                  </p>
                  <button
                    data-testid="forgot-back-to-login-button"
                    onClick={() => {
                      setShowForgot(false);
                      setForgotSent(false);
                      setForgotEmail("");
                    }}
                    class="text-primary cursor-pointer border-0 bg-transparent text-[12px] font-medium hover:underline"
                  >
                    Back to login
                  </button>
                </div>
              }
            >
              <form onSubmit={handleForgotSubmit} class="space-y-5">
                <p class="text-on-surface-variant text-[12.5px]">
                  Enter your email and we'll send a password reset link.
                </p>
                <FormField
                  id="forgot-email"
                  label="Email"
                  type="text"
                  placeholder="your@email.com"
                  value={forgotEmail()}
                  onInput={e => setForgotEmail(e.currentTarget.value)}
                  required
                  autofocus
                  leftIcon="alternate_email"
                  testId="forgot-email-input"
                />
                <div class="flex justify-end gap-3 pt-1">
                  <Button
                    data-testid="forgot-submit-button"
                    type="submit"
                    disabled={forgotMutation.isPending || !forgotEmail()}
                  >
                    {forgotMutation.isPending ? "Sending…" : "Send Reset Link"}
                  </Button>
                  <Button
                    data-testid="forgot-cancel-button"
                    type="button"
                    variant="outline"
                    onClick={() => {
                      setShowForgot(false);
                      setError("");
                    }}
                  >
                    <span class="material-symbols-outlined text-[18px]">close</span>
                    Cancel
                  </Button>
                </div>
              </form>
            </Show>
          </AuthCard>
        }
      >
        <AuthCard
          title="Welcome Back"
          error={error()}
          testId="login-card"
          headingTestId="login-heading"
        >
          <form onSubmit={handleSubmit} class="space-y-5">
            <FormField
              id="email"
              label="Email"
              type="email"
              placeholder="your@email.com"
              value={email()}
              onInput={e => setEmail(e.currentTarget.value)}
              required
              autofocus
              autocomplete="email"
              leftIcon="alternate_email"
              testId="login-email-input"
            />
            <FormField
              id="password"
              label="Password"
              type="password"
              placeholder="••••••••••••"
              value={password()}
              onInput={e => setPassword(e.currentTarget.value)}
              required
              autocomplete="current-password"
              leftIcon="key"
              testId="login-password-input"
            />
            <div class="pt-2">
              <label class="group mb-4 flex w-fit cursor-pointer items-center gap-2.5">
                <div
                  class={`flex h-4 w-4 shrink-0 items-center justify-center rounded border-2 transition-all ${
                    rememberMe()
                      ? "bg-primary border-primary"
                      : "border-outline-variant/50 group-hover:border-outline"
                  }`}
                  onClick={() => setRememberMe(v => !v)}
                >
                  <Show when={rememberMe()}>
                    <span class="material-symbols-outlined text-on-primary text-[11px]">check</span>
                  </Show>
                </div>
                <span
                  class="text-on-surface-variant text-[12px] select-none"
                  onClick={() => setRememberMe(v => !v)}
                >
                  Remember me on this device
                </span>
              </label>
              <Button
                data-testid="login-submit-button"
                type="submit"
                size="lg"
                disabled={isBusy()}
                class="w-full"
              >
                <span>{loginMutation.isPending ? "Signing in…" : "Login to Console"}</span>
                <span class="material-symbols-outlined text-[18px]">login</span>
              </Button>
            </div>
          </form>

          <div class="mt-5 flex items-center justify-between text-[12px]">
            <span class="text-on-surface-variant">
              Don't have an account?{" "}
              <A
                href="/register"
                class="text-primary hover:text-primary/80 font-bold transition-colors hover:underline"
              >
                Register
              </A>
            </span>
            <button
              data-testid="forgot-open-button"
              type="button"
              onClick={() => {
                setShowForgot(true);
                setForgotEmail(email());
                setError("");
              }}
              class="text-outline hover:text-primary cursor-pointer border-0 bg-transparent transition-colors"
            >
              Forgot password?
            </button>
          </div>

          <SsoSection
            ssoConfig={ssoConfigQuery.data}
            isBusy={isBusy()}
            onSsoSuccess={handleSsoSuccess}
            onSsoError={msg => setError(msg)}
          />

          <div class="border-outline-variant/15 mt-6 border-t pt-5">
            <div class="text-outline flex items-center justify-center gap-6 text-[10px] font-medium">
              <a
                class="hover:text-primary flex items-center gap-1.5 transition-colors"
                href="https://www.nonaconfig.com/support"
                target="_blank"
              >
                <span class="material-symbols-outlined text-[15px]">contact_support</span>
                Support
              </a>
              <a
                class="hover:text-primary flex items-center gap-1.5 transition-colors"
                href="https://www.nonaconfig.com/docs"
                target="_blank"
              >
                <span class="material-symbols-outlined text-[15px]">terminal</span>
                API Docs
              </a>
            </div>
          </div>
        </AuthCard>
      </Show>
    </>
  );
}

function getErrorMessage(caught: unknown, fallback: string) {
  if (caught instanceof ApiRequestError && caught.code === "sso_user_not_registered") {
    return "This account is not registered in the app. Ask an administrator to create your account before using SSO.";
  }

  if (caught instanceof Error && caught.message) {
    return caught.message;
  }

  return fallback;
}
