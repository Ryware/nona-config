export type ActionKind = "create" | "update" | "delete" | "system";

export function getActionKind(action: string): ActionKind {
  if (action.includes("Created") || action.includes("Invited")) return "create";
  if (action.includes("Updated") || action.includes("changed")) return "update";
  if (action.includes("Deleted") || action.includes("Deleted Key")) return "delete";
  return "system";
}

export function truncate(str: string | undefined, max = 28): string {
  if (!str) return "";
  return str.length > max ? str.slice(0, max) + "…" : str;
}

/* ── Action category helpers ── */

const PARAM_ACTIONS: ReadonlySet<string> = new Set([
  "Created Parameter",
  "Updated Parameter",
  "Deleted Parameter",
  "Deleted Key",
]);

export function isParamAction(action: string): boolean {
  return PARAM_ACTIONS.has(action);
}

export function isUpdateAction(action: string): boolean {
  return action === "Updated Parameter";
}

export function isCreateAction(action: string): boolean {
  return action === "Created Parameter";
}

export function isDeleteAction(action: string): boolean {
  return action === "Deleted Parameter" || action === "Deleted Key";
}

/* ── Human-readable action descriptions ── */

interface ActionDescription {
  text: string;
  colorClass: string;
}

const ACTION_DESCRIPTIONS: Readonly<Record<string, ActionDescription>> = {
  "Created Project": { text: "New project created", colorClass: "text-outline/70" },
  "Updated Project": { text: "Project settings modified", colorClass: "text-outline/70" },
  "Invited User": { text: "Joined as team member", colorClass: "text-secondary/80" },
};

export function getActionDescription(action: string): ActionDescription | null {
  if (isParamAction(action)) return null;
  return ACTION_DESCRIPTIONS[action] ?? { text: action, colorClass: "text-outline/70" };
}
