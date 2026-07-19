import { For, Show } from "solid-js";

interface UserRoleSelectorProps {
  role: "editor" | "viewer";
  onChange: (role: "editor" | "viewer") => void;
}

export function UserRoleSelector(props: UserRoleSelectorProps) {
  const ROLE_CARDS = [
    {
      value: "editor" as const,
      icon: "shield_person",
      label: "Editor",
      desc: "Full access to modify projects, manage team members, and view billing logs."
    },
    {
      value: "viewer" as const,
      icon: "visibility",
      label: "Viewer",
      desc: "Read-only access to configurations and logs. Cannot modify environment variables."
    }
  ];

  return (
    <section class="bg-surface-container-low border-outline-variant/15 space-y-5 rounded-xl border p-4 shadow-sm sm:p-8">
      <div class="flex items-center gap-3">
        <div class="bg-primary/10 border-primary/20 text-primary flex h-7 w-7 shrink-0 items-center justify-center rounded-full border font-mono text-xs font-bold shadow-[0_0_12px_rgba(99,102,241,0.15)]">
          02
        </div>
        <h3 class="font-headline text-on-surface text-lg font-bold">Role Assignment</h3>
      </div>
      <div
        role="radiogroup"
        aria-label="System Role Assignment"
        class="grid grid-cols-1 gap-3 md:grid-cols-2 md:gap-4"
      >
        <For each={ROLE_CARDS}>
          {card => {
            const isSelected = () => props.role === card.value;
            return (
              <div
                data-testid={`invite-role-${card.value}`}
                role="radio"
                aria-checked={isSelected()}
                tabindex="0"
                onClick={() => props.onChange(card.value)}
                onKeyDown={e => {
                  if (e.key === "Enter" || e.key === " ") {
                    e.preventDefault();
                    props.onChange(card.value);
                  }
                }}
                class={`focus-visible:ring-primary/40 h-full cursor-pointer rounded-lg border p-4 transition-all duration-300 select-none focus-visible:ring-2 focus-visible:outline-none active:scale-[0.98] sm:p-6 ${
                  isSelected()
                    ? "border-primary bg-primary/5 scale-[1.01] shadow-[0_0_20px_rgba(99,102,241,0.03)]"
                    : "bg-surface-container-low border-outline-variant/15 hover:bg-surface-container-high/40 hover:border-outline-variant/30"
                }`}
              >
                <div class="mb-4 flex items-start justify-between">
                  <div
                    class={`flex h-10 w-10 items-center justify-center rounded-lg transition-all ${isSelected() ? "bg-primary/10 text-primary border-primary/25 border" : "bg-surface-container-high text-outline border-outline-variant/10 border"}`}
                  >
                    <span class="material-symbols-outlined text-xl">{card.icon}</span>
                  </div>
                  {/* Radio indicator */}
                  <div
                    class={`flex h-5 w-5 items-center justify-center rounded-full border-2 transition-all ${
                      isSelected() ? "border-primary bg-primary" : "border-outline-variant"
                    }`}
                  >
                    <Show when={isSelected()}>
                      <div class="bg-on-primary h-1.5 w-1.5 rounded-full" />
                    </Show>
                  </div>
                </div>
                <div class="font-headline text-on-surface text-base font-bold">{card.label}</div>
                <p class="text-on-surface-variant mt-2 text-xs leading-relaxed">{card.desc}</p>
              </div>
            );
          }}
        </For>
      </div>
    </section>
  );
}
