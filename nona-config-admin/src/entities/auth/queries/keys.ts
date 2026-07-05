export const authKeys = {
  all: () => ["auth"] as const,
  firstTime: () => [...authKeys.all(), "first-time"] as const,
  ssoConfig: () => [...authKeys.all(), "sso-config"] as const,
  invitation: (token: string) =>
    [...authKeys.all(), "invitation", token] as const,
} as const;
