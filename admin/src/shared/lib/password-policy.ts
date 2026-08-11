import { MSG } from "./messages";

export interface PasswordRequirement {
  label: string;
  error: string;
  test: (password: string) => boolean;
}

export const PASSWORD_REQUIREMENTS: readonly PasswordRequirement[] = [
  {
    label: "At least 8 characters",
    error: MSG.PASSWORD_MIN_LENGTH,
    test: password => password.length >= 8
  },
  {
    label: "One uppercase letter",
    error: MSG.PASSWORD_UPPERCASE_REQUIRED,
    test: password => /[A-Z]/.test(password)
  },
  {
    label: "One number",
    error: MSG.PASSWORD_NUMBER_REQUIRED,
    test: password => /[0-9]/.test(password)
  },
  {
    label: "One special character",
    error: MSG.PASSWORD_SPECIAL_REQUIRED,
    test: password => /[^A-Za-z0-9]/.test(password)
  }
];

export function getPasswordPolicyError(password: string): string | null {
  if (!password) return MSG.PASSWORD_REQUIRED;
  return PASSWORD_REQUIREMENTS.find(requirement => !requirement.test(password))?.error ?? null;
}

export function validateNewPassword(password: string, confirmation: string): string | null {
  if (password !== confirmation) return MSG.PASSWORD_MISMATCH;
  return getPasswordPolicyError(password);
}
