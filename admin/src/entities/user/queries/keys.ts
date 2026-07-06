export const userKeys = {
  all: () => ["user"] as const,
  list: () => [...userKeys.all(), "list"] as const,
  detail: (id: string) => [...userKeys.all(), "detail", id] as const,
} as const;
