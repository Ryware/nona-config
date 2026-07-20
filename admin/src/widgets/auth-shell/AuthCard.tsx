import { type Component, type JSX, Show } from "solid-js";

interface AuthCardProps {
  title: string;
  description?: string;
  children: JSX.Element;
  footer?: JSX.Element;
  error?: string;
  testId?: string;
  headingTestId?: string;
}

export const AuthCard: Component<AuthCardProps> = props => {
  return (
    <div
      data-testid={props.testId}
      class="animate-fade-in min-h-screen w-full max-w-none overflow-hidden rounded-none border-0 bg-transparent shadow-none sm:min-h-0 sm:max-w-105 sm:rounded-2xl sm:border sm:border-outline-variant/15 sm:bg-surface-container-low sm:shadow-[0_0_50px_rgba(0,0,0,0.3)]"
    >
      <div class="flex min-h-screen flex-col justify-start px-5 pb-8 pt-22 sm:min-h-0 sm:justify-center sm:p-8 md:p-10">
        <div class="mx-auto w-full max-w-sm sm:max-w-none">
          {/* Brand Header */}
          <div class="mb-8 flex flex-col items-start gap-3 text-left sm:items-center sm:text-center">
            <div class="bg-primary/10 border-primary/20 text-primary flex h-11 w-11 items-center justify-center rounded-2xl border shadow-[0_0_15px_rgba(99,102,241,0.15)] sm:h-10 sm:w-10 sm:rounded-xl">
              <span
                class="material-symbols-outlined text-lg font-bold"
                style={{ "font-variation-settings": "'FILL' 1, 'wght' 400, 'GRAD' 0, 'opsz' 24" }}
              >
                terminal
              </span>
            </div>
            <span class="font-headline text-on-surface text-xl font-bold tracking-tight">
              Nona Config
            </span>
          </div>

          {/* Title */}
          <header class="mb-8 text-left sm:text-center">
            <h2
              data-testid={props.headingTestId}
              class="font-headline text-on-surface mb-2 text-[1.65rem] leading-tight font-bold tracking-tight sm:text-xl"
            >
              {props.title}
            </h2>
            <Show when={props.description}>
              <p class="text-on-surface-variant max-w-[32ch] text-[13px] leading-relaxed sm:max-w-none sm:text-xs">
                {props.description}
              </p>
            </Show>
          </header>

          {/* Error */}
          <Show when={props.error}>
            <div class="text-error bg-error-container/10 border-error/25 mb-6 flex items-center gap-2 rounded-xl border p-3 text-xs">
              <span class="material-symbols-outlined shrink-0 text-[16px]">error</span>
              <span class="font-medium">{props.error}</span>
            </div>
          </Show>

          {/* Body */}
          {props.children}

          {/* Footer */}
          <Show when={props.footer}>
            <div class="mt-6">{props.footer}</div>
          </Show>
        </div>
      </div>
    </div>
  );
};
