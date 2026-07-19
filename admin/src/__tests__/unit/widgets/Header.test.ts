import { describe, expect, it } from "vitest";
import { getProjectNavigationPath } from "../../../widgets/app-shell/Header";

describe("getProjectNavigationPath", () => {
  it("preserves the current project section when switching projects", () => {
    expect(getProjectNavigationPath("/projects/my-app", "backend-api")).toBe(
      "/projects/backend-api"
    );
    expect(
      getProjectNavigationPath("/projects/my-app/environments", "backend-api")
    ).toBe("/projects/backend-api/environments");
    expect(
      getProjectNavigationPath("/projects/my-app/shared-links", "backend-api")
    ).toBe("/projects/backend-api/shared-links");
    expect(getProjectNavigationPath("/projects/my-app/api-keys", "backend-api")).toBe(
      "/projects/backend-api/api-keys"
    );
    expect(getProjectNavigationPath("/projects/my-app/releases", "backend-api")).toBe(
      "/projects/backend-api/releases"
    );
  });

  it("falls back to the project page for non-project sections", () => {
    expect(getProjectNavigationPath("/users", "backend-api")).toBe(
      "/projects/backend-api"
    );
  });
});
