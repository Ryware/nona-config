import { Show } from "solid-js";
import { Input } from "../../../shared/ui/input";

interface UserInviteLinkProps {
  email: string;
  invitationUrl: string;
  copyFeedback: string;
  onCopy: () => void;
  onBack: () => void;
}

export function UserInviteLink(props: UserInviteLinkProps) {
  return (
    <section
      data-testid="invite-link-section"
      class="bg-primary/5 border-primary/20 animate-fade-in space-y-6 rounded-xl border p-8 shadow-[0_0_30px_rgba(99,102,241,0.02)]"
    >
      <div class="flex items-center gap-3">
        <span class="bg-primary/10 text-primary border-primary/20 rounded-full border px-2.5 py-0.5 font-mono text-[10px] font-bold tracking-wider">
          READY
        </span>
        <h3
          data-testid="invite-link-heading"
          class="font-headline text-on-surface text-lg font-bold"
        >
          Invitation Link
        </h3>
      </div>
      <p class="text-on-surface-variant text-sm leading-relaxed">
        Send this link to <span class="text-on-surface font-semibold">{props.email}</span>. It can
        be used once to create a password or finish sign-in with SSO.
      </p>
      <div class="flex flex-col gap-3 md:flex-row">
        <Input
          data-testid="invite-link-input"
          type="text"
          readOnly
          value={props.invitationUrl}
          class="flex-1 font-mono"
          leftIcon="link"
        />
        <button
          data-testid="invite-link-copy-button"
          type="button"
          onClick={() => props.onCopy()}
          class="bg-primary text-on-primary cursor-pointer rounded-lg border-0 px-6 py-3 text-sm font-bold shadow-md transition-all hover:brightness-105"
        >
          Copy Link
        </button>
      </div>
      <Show when={props.copyFeedback}>
        <p class="text-primary text-xs font-medium">{props.copyFeedback}</p>
      </Show>
      <div class="flex gap-3 pt-2">
        <button
          type="button"
          onClick={() => props.onBack()}
          class="border-outline-variant/15 bg-surface-container-low text-on-surface-variant hover:text-on-surface cursor-pointer rounded-lg border px-5 py-2.5 text-xs font-bold transition-colors"
        >
          Back to Team Overview
        </button>
      </div>
    </section>
  );
}
