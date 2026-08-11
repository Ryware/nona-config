import { describe, expect, it } from "vitest";
import { getPasswordPolicyError, validateNewPassword } from "../../shared/lib/password-policy";

describe("password policy", () => {
  it("accepts a password that meets every requirement", () => {
    expect(getPasswordPolicyError("Password1!")).toBeNull();
    expect(validateNewPassword("Password1!", "Password1!")).toBeNull();
  });

  it.each([
    ["", "Password is required."],
    ["Short1!", "Password must be at least 8 characters long."],
    ["password1!", "Password must contain at least one uppercase letter."],
    ["Password!", "Password must contain at least one number."],
    ["Password1", "Password must contain at least one special character."]
  ])("rejects %j with the expected error", (password, expectedError) => {
    expect(getPasswordPolicyError(password)).toBe(expectedError);
  });

  it("reports a confirmation mismatch before policy errors", () => {
    expect(validateNewPassword("weak", "different")).toBe("Passwords do not match.");
  });
});
