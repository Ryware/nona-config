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
      class="border-outline-variant/15 bg-surface-container-low animate-fade-in w-full max-w-105 overflow-hidden rounded-2xl border shadow-[0_0_50px_rgba(0,0,0,0.3)]"
    >
      <div class="flex flex-col justify-center p-8 md:p-10">
        <div class="mx-auto w-full">
          {/* Brand Header */}
          <div class="mb-8 flex flex-col items-center gap-3 text-center">
            <div class="bg-primary/10 border-primary/20 text-primary flex h-10 w-10 items-center justify-center rounded-xl border shadow-[0_0_15px_rgba(99,102,241,0.15)]">
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
          <header class="mb-8 text-center">
            <h2
              data-testid={props.headingTestId}
              class="font-headline text-on-surface mb-2 text-xl font-bold tracking-tight"
            >
              {props.title}
            </h2>
            <Show when={props.description}>
              <p class="text-on-surface-variant text-xs leading-relaxed">{props.description}</p>
            </Show>
          </header>

          {/* Error */}
          <Show when={props.error}>
            <div class="text-error bg-error-container/10 border-error/25 mb-6 flex items-center gap-2 rounded-lg border p-3 text-xs">
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
