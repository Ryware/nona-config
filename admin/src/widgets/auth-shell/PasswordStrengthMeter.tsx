import { createMemo, For, Show } from "solid-js";
import { PASSWORD_REQUIREMENTS } from "../../shared/lib/password-policy";

interface PasswordStrengthMeterProps {
  password: string;
}

export function PasswordStrengthMeter(props: PasswordStrengthMeterProps) {
  const metCount = createMemo(
    () => PASSWORD_REQUIREMENTS.filter(requirement => requirement.test(props.password || "")).length
  );

  const strengthLabel = createMemo(() => {
    const n = metCount();
    if (n === 0) return { label: "", bar: "bg-outline-variant/30", badge: "", badgeText: "" };
    if (n <= 1) return { label: "Weak",   bar: "bg-error",      badge: "bg-error/10 text-error",      badgeText: "text-error"      };
    if (n === 2) return { label: "Fair",   bar: "bg-warning",    badge: "bg-warning/10 text-warning",  badgeText: "text-warning"    };
    if (n === 3) return { label: "Good",   bar: "bg-secondary",  badge: "bg-secondary/10 text-secondary", badgeText: "text-secondary" };
    return               { label: "Strong", bar: "bg-success",   badge: "bg-success/10 text-success",  badgeText: "text-success"    };
  });

  return (
    <Show when={(props.password || "").length > 0}>
      <div class="space-y-2 mt-1">
        <div class="flex gap-1">
          <For each={PASSWORD_REQUIREMENTS}>
            {(_, i) => (
              <div
                class={`h-1 flex-1 rounded-full transition-all duration-300 ${
                  i() < metCount() ? strengthLabel().bar : "bg-outline-variant/20"
                }`}
              />
            )}
          </For>
        </div>
        <div class="flex items-center justify-between">
          <div class="space-y-0.5">
            <For each={PASSWORD_REQUIREMENTS}>
              {(req) => (
                <div
                  class={`flex items-center gap-1.5 text-[10px] transition-colors ${
                    req.test(props.password || "") ? "text-success" : "text-outline"
                  }`}
                >
                  <span class="material-symbols-outlined text-[12px]">
                    {req.test(props.password || "") ? "check_circle" : "radio_button_unchecked"}
                  </span>
                  {req.label}
                </div>
              )}
            </For>
          </div>
          <Show when={strengthLabel().label}>
            <span
              class={`text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded-full ${
                strengthLabel().badge
              }`}
            >
              {strengthLabel().label}
            </span>
          </Show>
        </div>
      </div>
    </Show>
  );
}
