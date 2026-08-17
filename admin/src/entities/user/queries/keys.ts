export const userKeys = {
  all: () => ["user"] as const,
  list: () => [...userKeys.all(), "list"] as const,
  detail: (id: string | number) => [...userKeys.all(), "detail", String(id)] as const,
} as const;
