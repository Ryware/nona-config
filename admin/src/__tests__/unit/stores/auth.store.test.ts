import { beforeEach, describe, expect, it } from "vitest";
import { authStore } from "../../../entities/auth/model/store";
import { queryClient } from "../../../shared/api/query-client";

describe("authStore", () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
  });

  it("clears this tab's cache when another tab changes the remembered identity", async () => {
    const { vi } = await import("vitest");
    const reload = vi.spyOn(window.location, "reload").mockImplementation(() => {});
    try {
      authStore.saveSession("admin", { email: "admin@example.com", role: "admin" });
      queryClient.setQueryData(["private"], "admin secret");
      localStorage.setItem("auth_token", "member");
      authStore.handleStorageChange(
        new StorageEvent("storage", {
          key: "auth_token",
          oldValue: "admin",
          newValue: "member",
          storageArea: localStorage
        })
      );
      expect(queryClient.getQueryData(["private"])).toBeUndefined();
      expect(authStore.getToken()).toBe("member");
      expect(reload).toHaveBeenCalledOnce();
      queryClient.setQueryData(["private"], "cached");
      authStore.handleStorageChange(
        new StorageEvent("storage", { key: null, storageArea: localStorage })
      );
      expect(queryClient.getQueryData(["private"])).toBeUndefined();
    } finally {
      reload.mockRestore();
    }
  });

  it("clears cached secrets and both storage tiers when changing identity", () => {
    authStore.saveSession("admin-token", { email: "admin@example.com", role: "admin" });
    queryClient.setQueryData(["project", "detail", "private", "config-entries", "production"], {
      secret: "admin-only"
    });
    authStore.clearSession();
    expect(queryClient.getQueryCache().getAll()).toHaveLength(0);
    authStore.saveSession("member-token", { email: "member@example.com", role: "member" }, false);
    expect(authStore.getToken()).toBe("member-token");
    expect(localStorage.getItem("auth_token")).toBeNull();
    expect(queryClient.getQueryCache().getAll()).toHaveLength(0);
  });

  it("does not restore an old session's cache when its pending request completes", async () => {
    let finish!: (value: string) => void;
    const pending = queryClient
      .fetchQuery({
        queryKey: ["private"],
        queryFn: () =>
          new Promise<string>(resolve => {
            finish = resolve;
          })
      })
      .catch(() => undefined);
    authStore.saveSession("new-token", { email: "member@example.com", role: "member" }, false);
    finish("old secret");
    await pending;
    expect(queryClient.getQueryData(["private"])).toBeUndefined();
    expect(queryClient.getQueryCache().getAll()).toHaveLength(0);
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

describe("responses from a previous identity", () => {
  it("rejects a late mutation response before it can repopulate caches", async () => {
    const { ApiClient } = await import("../../../shared/api/client");
    const { vi } = await import("vitest");
    authStore.saveSession("old", { email: "admin@example.com", role: "admin" });
    let complete!: (response: Response) => void;
    const fetchSpy = vi.spyOn(globalThis, "fetch").mockImplementation(
      () =>
        new Promise(resolve => {
          complete = resolve;
        })
    );
    try {
      const pending = new ApiClient().put("/admin/private", { value: "old" });
      authStore.saveSession("new", { email: "member@example.com", role: "member" });
      complete(new Response(JSON.stringify({ secret: "old response" }), { status: 200 }));
      await expect(pending).rejects.toMatchObject({ name: "AbortError" });
      expect(authStore.getToken()).toBe("new");
    } finally {
      fetchSpy.mockRestore();
    }
  });

  it("does not sign the new user out when an old request returns 401", async () => {
    const { ApiClient } = await import("../../../shared/api/client");
    const { vi } = await import("vitest");
    authStore.saveSession("old", { email: "admin@example.com", role: "admin" });
    let complete!: (response: Response) => void;
    const unauthorized = vi.fn();
    window.addEventListener("auth:unauthorized", unauthorized);
    const fetchSpy = vi.spyOn(globalThis, "fetch").mockImplementation(
      () =>
        new Promise(resolve => {
          complete = resolve;
        })
    );
    try {
      const pending = new ApiClient().get("/admin/private");
      authStore.clearSession();
      authStore.saveSession("new", { email: "member@example.com", role: "member" });
      complete(new Response("{}", { status: 401 }));
      await expect(pending).rejects.toMatchObject({ name: "AbortError" });
      expect(unauthorized).not.toHaveBeenCalled();
    } finally {
      fetchSpy.mockRestore();
      window.removeEventListener("auth:unauthorized", unauthorized);
    }
  });
});
