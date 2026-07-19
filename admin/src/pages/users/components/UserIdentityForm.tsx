import { Input } from "../../../shared/ui/input";
import { Label } from "../../../shared/ui/label";

interface UserIdentityFormProps {
  name: string;
  email: string;
  isEditMode: boolean;
  isEmailDisabled?: boolean;
  onNameChange: (val: string) => void;
  onEmailChange: (val: string) => void;
}

export function UserIdentityForm(props: UserIdentityFormProps) {
  return (
    <section class="bg-surface-container-low border-outline-variant/15 space-y-6 rounded-xl border p-8">
      <div class="flex items-center gap-3">
        <div class="bg-primary/10 border-primary/20 text-primary flex h-7 w-7 shrink-0 items-center justify-center rounded-full border font-mono text-xs font-bold shadow-[0_0_12px_rgba(99,102,241,0.15)]">
          01
        </div>
        <h3 class="font-headline text-on-surface text-lg font-bold">
          {props.isEditMode ? "Member Identity" : "Invitee Identity"}
        </h3>
      </div>
      <div class="grid grid-cols-1 gap-6 md:grid-cols-2">
        <div class="space-y-2">
          <Label for="user-full-name" class="mb-0">
            Full Name or Alias
          </Label>
          <Input
            data-testid="invite-name-input"
            id="user-full-name"
            type="text"
            placeholder="e.g. John Smith"
            value={props.name}
            onInput={(e: InputEvent & { currentTarget: HTMLInputElement }) =>
              props.onNameChange(e.currentTarget.value)
            }
            required={!props.isEditMode}
            leftIcon="person"
          />
        </div>
        <div class="space-y-2">
          <Label for="user-email-address" class="mb-0">
            Email Address <span class="text-primary">*</span>
          </Label>
          <Input
            data-testid="invite-email-input"
            id="user-email-address"
            type="email"
            placeholder="alex@company.com"
            value={props.email}
            onInput={(e: InputEvent & { currentTarget: HTMLInputElement }) =>
              props.onEmailChange(e.currentTarget.value)
            }
            disabled={props.isEmailDisabled}
            required
            autofocus={!props.isEditMode}
            leftIcon="alternate_email"
          />
        </div>
      </div>
    </section>
  );
}
