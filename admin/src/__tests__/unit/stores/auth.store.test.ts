import { beforeEach, describe, expect, it } from "vitest";
import { authStore } from "../../../entities/auth/model/store";

describe("authStore", () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
  });

  it("stores an explicit admin role without an admin flag", () => {
    authStore.saveSession("opaque-token", { email: "admin@example.com", role: "admin" });

    expect(JSON.parse(localStorage.getItem("auth_session") ?? "{}")).toEqual({
      email: "admin@example.com",
      role: "admin"
    });
  });

  it("rejects legacy organization roles", () => {
    localStorage.setItem("auth_token", "opaque-token");
    localStorage.setItem(
      "auth_session",
      JSON.stringify({ email: "admin@example.com", role: "viewer", isAdmin: true })
    );

    expect(authStore.getSession()).toBeNull();
  });
});
