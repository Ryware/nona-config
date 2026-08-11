import { type RouteSectionProps, useNavigate, useSearchParams } from "@solidjs/router";
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

const SSO_REMEMBER_ME_KEY = "nona:sso:remember-me";

export default function LoginPage(props: LoginPageProps = {}) {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [email, setEmail] = createSignal("");
  const [password, setPassword] = createSignal("");
  const [error, setError] = createSignal("");
  const [rememberMe, setRememberMe] = createSignal(loadSsoRememberMePreference());

  const completeLogin = (result: LoginResponse) => {
    sessionStorage.removeItem(SSO_REMEMBER_ME_KEY);
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
      <AuthCard
        title="Welcome Back"
        error={error()}
        testId="login-card"
        headingTestId="login-heading"
      >
        <Show when={searchParams.passwordReset === "success"}>
          <div
            data-testid="password-reset-success"
            class="bg-success/10 text-success border-success/20 mb-5 flex items-start gap-2 rounded-xl border px-3.5 py-3 text-xs"
          >
            <span class="material-symbols-outlined text-[17px]">check_circle</span>
            <span>{MSG.PASSWORD_RESET_COMPLETE}</span>
          </div>
        </Show>
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

        <SsoSection
          ssoConfig={ssoConfigQuery.data}
          isBusy={isBusy()}
          onSsoSuccess={handleSsoSuccess}
          onSsoError={msg => setError(msg)}
          onRedirectStart={() => {
            sessionStorage.setItem(SSO_REMEMBER_ME_KEY, String(rememberMe()));
          }}
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

function loadSsoRememberMePreference() {
  return sessionStorage.getItem(SSO_REMEMBER_ME_KEY) !== "false";
}
