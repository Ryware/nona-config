import { createEventListener } from "@solid-primitives/event-listener";
import type { JSXElement } from "solid-js";
import { Show } from "solid-js";

interface ConfirmDialogProps {
  open: boolean;
  title: string;
  message: string | JSXElement;
  confirmLabel?: string;
  cancelLabel?: string;
  variant?: "danger" | "warning" | "info";
  isLoading?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
  testId?: string;
  confirmTestId?: string;
  cancelTestId?: string;
}

const VARIANT = {
  danger: {
    icon: "warning",
    iconClass: "text-error",
    bannerClass: "bg-error-container/10 border-error/25",
    titleClass: "text-error",
    btnClass: "bg-error text-on-error hover:brightness-105 shadow-md"
  },
  warning: {
    icon: "warning",
    iconClass: "text-amber-400",
    bannerClass: "bg-amber-500/10 border-amber-500/25",
    titleClass: "text-amber-400",
    btnClass: "bg-amber-500 text-white hover:brightness-105 shadow-md"
  },
  info: {
    icon: "info",
    iconClass: "text-primary",
    bannerClass: "bg-primary/10 border-primary/25",
    titleClass: "text-primary",
    btnClass: "bg-primary text-on-primary hover:brightness-105 shadow-md"
  }
} as const;

export const ConfirmDialog = (props: ConfirmDialogProps) => {
  const v = () => VARIANT[props.variant ?? "danger"];

  const handleKey = (e: KeyboardEvent) => {
    if (!props.open) return;
    if (e.key === "Escape") props.onCancel();
    if (e.key === "Enter" && !props.isLoading) props.onConfirm();
  };

  createEventListener(document, "keydown", handleKey);

  return (
    <Show when={props.open}>
      <div
        data-testid={props.testId}
        class="animate-backdrop-in fixed inset-0 z-80 flex items-center justify-center bg-black/60 backdrop-blur-sm"
        role="dialog"
        aria-modal="true"
        aria-labelledby="confirm-dialog-title"
        onClick={e => {
          if (e.target === e.currentTarget) props.onCancel();
        }}
      >
        <div class="bg-surface-container-low border-outline-variant/15 animate-palette-in mx-4 w-full max-w-sm rounded-2xl border p-8 shadow-2xl">
          {/* Banner */}
          <div class={`mb-6 flex items-center gap-3 rounded-lg border p-3 ${v().bannerClass}`}>
            <span class={`material-symbols-outlined text-[20px] ${v().iconClass}`}>{v().icon}</span>
            <h3 id="confirm-dialog-title" class={`text-[13px] font-semibold ${v().titleClass}`}>
              {props.title}
            </h3>
          </div>

          {/* Message */}
          <div class="text-on-surface-variant mb-6 text-sm leading-relaxed">{props.message}</div>

          {/* Actions */}
          <div class="flex gap-3">
            <button
              data-testid={props.confirmTestId}
              onClick={() => props.onConfirm()}
              disabled={props.isLoading}
              class={`flex flex-1 cursor-pointer items-center justify-center gap-2 rounded-lg border-0 py-2.5 text-[13px] font-semibold transition-all disabled:opacity-50 ${v().btnClass}`}
            >
              <Show when={props.isLoading}>
                <span class="material-symbols-outlined animate-spin text-[14px]">
                  progress_activity
                </span>
              </Show>
              {props.isLoading ? "Please wait…" : (props.confirmLabel ?? "Confirm")}
            </button>
            <button
              data-testid={props.cancelTestId}
              onClick={() => props.onCancel()}
              disabled={props.isLoading}
              class="text-on-surface-variant bg-surface-container-high hover:bg-surface-bright flex flex-1 cursor-pointer items-center justify-center gap-2 rounded-lg border-0 py-2.5 text-[13px] font-semibold transition-all disabled:opacity-50"
            >
              <span class="material-symbols-outlined text-[14px]">close</span>
              {props.cancelLabel ?? "Cancel"}
            </button>
          </div>

          {/* Keyboard hint */}
          <p class="text-outline mt-4 text-center text-[10px]">
            Press{" "}
            <kbd class="bg-surface-container-high border-outline-variant/20 rounded border px-1 font-bold">
              Enter
            </kbd>{" "}
            to confirm &nbsp;·&nbsp;
            <kbd class="bg-surface-container-high border-outline-variant/20 rounded border px-1 font-bold">
              Esc
            </kbd>{" "}
            to cancel
          </p>
        </div>
      </div>
    </Show>
  );
};
