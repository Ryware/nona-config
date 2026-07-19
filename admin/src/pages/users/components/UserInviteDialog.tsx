import { Show } from "solid-js";
import { Portal } from "solid-js/web";
import { Button } from "../../../shared/ui/button";
import { MIcon } from "../../../shared/ui/icons";
import { Input } from "../../../shared/ui/input";

interface UserInviteDialogProps {
  open: boolean;
  email: string;
  invitationUrl: string;
  copyFeedback: string;
  onCopy: () => void;
  onClose: () => void;
}

export function UserInviteDialog(props: UserInviteDialogProps) {
  return (
    <Show when={props.open}>
      <Portal>
        <div
          data-testid="invite-link-dialog"
          class="animate-backdrop-in fixed inset-0 z-80 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm"
          role="dialog"
          aria-modal="true"
          aria-labelledby="invite-link-dialog-title"
          onClick={e => {
            if (e.target === e.currentTarget) props.onClose();
          }}
        >
          <div class="bg-surface-container-low border-outline-variant/15 animate-palette-in w-full max-w-lg rounded-2xl border p-8 shadow-2xl">
            <div class="mb-5 flex items-center gap-3">
              <span class="bg-primary/10 text-primary border-primary/20 rounded-full border px-2.5 py-0.5 font-mono text-[10px] font-bold tracking-wider">
                READY
              </span>
              <h3
                id="invite-link-dialog-title"
                data-testid="invite-link-heading"
                class="font-headline text-on-surface text-base font-bold"
              >
                Invitation Link
              </h3>
            </div>

            <p class="text-on-surface-variant mb-5 text-sm leading-relaxed">
              Send this link to{" "}
              <span class="text-on-surface font-semibold">{props.email}</span>. It can be used once
              to create a password or finish sign-in with SSO.
            </p>

            <div class="flex flex-col gap-3 sm:flex-row">
              <Input
                data-testid="invite-link-input"
                type="text"
                readOnly
                value={props.invitationUrl}
                class="flex-1 font-mono"
                leftIcon="link"
              />
              <Button
                data-testid="invite-link-copy-button"
                type="button"
                variant="secondary"
                onClick={() => props.onCopy()}
              >
                <MIcon name="content_copy" class="text-[16px]" />
                Copy Link
              </Button>
            </div>

            <Show when={props.copyFeedback}>
              <p class="text-primary mt-3 text-xs font-medium">{props.copyFeedback}</p>
            </Show>

            <div class="mt-6 flex justify-end gap-3">
              <Button
                data-testid="invite-link-close-button"
                type="button"
                variant="outline"
                onClick={() => props.onClose()}
              >
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
