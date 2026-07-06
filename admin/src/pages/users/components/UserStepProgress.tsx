import { Show } from "solid-js";

interface UserStepProgressProps {
  name: string;
  email: string;
  role: string;
}

export function UserStepProgress(props: UserStepProgressProps) {
  const isIdentityDone = () => !!(props.name.trim() && props.email.trim());
  const isRoleDone = () => !!props.role;

  return (
    <div class="flex items-center gap-2 mb-10 p-4 bg-surface-container-low rounded-xl border border-outline-variant/15">
      {/* Step 1: Identity */}
      <div class={`flex items-center gap-2 flex-1 ${isIdentityDone() ? "text-primary" : "text-outline"}`}>
        <div class={`w-6 h-6 rounded-full flex items-center justify-center text-[10px] font-bold shrink-0 border transition-all ${
          isIdentityDone()
            ? "bg-primary/10 border-primary/30 text-primary"
            : "bg-surface-container-high border-outline-variant/20 text-outline"
        }`}>
          <Show when={isIdentityDone()} fallback="01">
            <span class="material-symbols-outlined text-[13px]">check</span>
          </Show>
        </div>
        <span class="text-[11px] font-bold uppercase tracking-wider hidden sm:block">Identity</span>
      </div>

      <div class={`flex-1 h-px transition-all ${isIdentityDone() ? "bg-primary/30" : "bg-outline-variant/20"}`} />

      {/* Step 2: Role */}
      <div class={`flex items-center gap-2 flex-1 ${isRoleDone() ? "text-primary" : "text-outline"}`}>
        <div class={`w-6 h-6 rounded-full flex items-center justify-center text-[10px] font-bold shrink-0 border transition-all ${
          isRoleDone()
            ? "bg-primary/10 border-primary/30 text-primary"
            : "bg-surface-container-high border-outline-variant/20 text-outline"
        }`}>
          <Show when={isRoleDone()} fallback="02">
            <span class="material-symbols-outlined text-[13px]">check</span>
          </Show>
        </div>
        <span class="text-[11px] font-bold uppercase tracking-wider hidden sm:block">Role</span>
      </div>

      <div class={`flex-1 h-px transition-all ${isRoleDone() ? "bg-primary/30" : "bg-outline-variant/20"}`} />

      {/* Step 3: Scope */}
      <div class="flex items-center gap-2 flex-1 text-primary">
        <div class="w-6 h-6 rounded-full flex items-center justify-center text-[10px] font-bold shrink-0 border transition-all bg-primary/10 border-primary/30 text-primary">
          <span class="material-symbols-outlined text-[13px]">check</span>
        </div>
        <span class="text-[11px] font-bold uppercase tracking-wider hidden sm:block">Scope</span>
      </div>
    </div>
  );
}
