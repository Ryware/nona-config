import { Show } from "solid-js";
import { Portal } from "solid-js/web";
import { Button } from "../../../shared/ui/button";
import { MIcon } from "../../../shared/ui/icons";
import { Input } from "../../../shared/ui/input";

interface PasswordResetDialogProps {
  open: boolean;
  email: string;
  resetUrl: string;
  expiresAt?: string;
  copyFeedback: string;
  onCopy: () => void;
  onClose: () => void;
}

export function PasswordResetDialog(props: PasswordResetDialogProps) {
  return (
    <Show when={props.open}>
      <Portal>
        <div
          data-testid="password-reset-link-dialog"
          class="animate-backdrop-in fixed inset-0 z-80 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm"
          role="dialog"
          aria-modal="true"
          aria-labelledby="password-reset-link-dialog-title"
          onClick={event => {
            if (event.target === event.currentTarget) props.onClose();
          }}
        >
          <div class="bg-surface-container-low border-outline-variant/15 animate-palette-in w-full max-w-lg rounded-2xl border p-8 shadow-2xl">
            <div class="mb-5 flex items-center gap-3">
              <span class="bg-warning/10 text-warning border-warning/20 rounded-full border px-2.5 py-0.5 font-mono text-[10px] font-bold tracking-wider">
                24 HOURS
              </span>
              <h3
                id="password-reset-link-dialog-title"
                class="font-headline text-on-surface text-base font-bold"
              >
                Password Reset Link
              </h3>
            </div>

            <p class="text-on-surface-variant mb-2 text-sm leading-relaxed">
              Send this link manually to <span class="text-on-surface font-semibold">{props.email}</span>.
              It can be used once to set a new password.
            </p>
            <Show when={props.expiresAt}>
              <p class="text-outline mb-5 text-xs">
                Expires {new Date(props.expiresAt!).toLocaleString()}.
              </p>
            </Show>

            <div class="flex flex-col gap-3 sm:flex-row">
              <Input
                data-testid="password-reset-link-input"
                type="text"
                readOnly
                value={props.resetUrl}
                class="flex-1 font-mono"
                leftIcon="link"
              />
              <Button type="button" variant="secondary" onClick={() => props.onCopy()}>
                <MIcon name="content_copy" class="text-[16px]" />
                Copy Link
              </Button>
            </div>

            <Show when={props.copyFeedback}>
              <p class="text-primary mt-3 text-xs font-medium">{props.copyFeedback}</p>
            </Show>

            <div class="mt-6 flex justify-end">
              <Button type="button" variant="outline" onClick={() => props.onClose()}>
                <MIcon name="close" class="text-[16px]" />
                Close
              </Button>
            </div>
          </div>
        </div>
      </Portal>
    </Show>
  );
}
