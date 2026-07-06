export function timeAgo(date: Date): string {
  const diff = Date.now() - date.getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return "just now";
  if (mins < 60) return `${mins}m ago`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `${hrs}h ago`;
  if (hrs < 48) return "Yesterday";
  return `${Math.floor(hrs / 24)}d ago`;
}

export function formatTimestamp(date: Date): string {
  return date.toLocaleDateString(undefined, { month: "short", day: "numeric" }) +
    ", " + date.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
}

export const ACTION_STYLE: Record<string, string> = {
  "Created Project": "bg-surface-container-highest text-on-surface-variant border border-outline-variant/30",
  "Updated Project": "bg-secondary-container/30 text-secondary border border-secondary/20",
  "Invited User":    "bg-surface-container-highest text-on-surface-variant border border-outline-variant/30",
  "Deleted Key":     "bg-error-container/20 text-error border border-error/20",
  "Auto-Scaling":    "bg-primary-container/20 text-primary border border-primary/20",
  "Created Parameter": "bg-success-container/20 text-success border border-success/20",
  "Updated Parameter": "bg-primary-container/20 text-primary border border-primary/20",
  "Deleted Parameter": "bg-error-container/20 text-error border border-error/20",
};

export const ENV_STYLE: Record<string, string> = {
  "Production":   "text-on-primary bg-primary",
  "Staging":      "text-on-secondary-container bg-secondary-container",
  "Global Scope": "text-outline-variant border border-outline-variant",
  "production":   "text-on-primary bg-primary",
  "staging":      "text-on-secondary-container bg-secondary-container",
};

export const ACTOR_ICON_COLORS = [
  { bg: "bg-primary/10",   text: "text-primary"   },
  { bg: "bg-secondary/10", text: "text-secondary" },
  { bg: "bg-error/10",     text: "text-error"     },
  { bg: "bg-surface-bright", text: "text-primary" },
];

export function actorStyle(str: string) {
  let h = 0;
  for (let i = 0; i < str.length; i++) h = (h * 31 + str.charCodeAt(i)) | 0;
  return ACTOR_ICON_COLORS[Math.abs(h) % ACTOR_ICON_COLORS.length];
}
