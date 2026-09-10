import { describe, expect, it } from "vitest";

import { getProjectPageSection } from "../../shared/lib/project-navigation";

describe("getProjectPageSection", () => {
  it.each([
    ["/projects/my-app", "", "parameters"],
    ["/projects/my-app", "?search=release", "parameters"],
    ["/projects/my-app", "?viewRelease=1.2.0", "releases"],
    ["/projects/my-app", "?release=1.3.0", "releases"],
    ["/projects/my-app", "?release=1.3.1&amend=1.3.0", "releases"],
    ["/projects/my-app", "?amend=1.3.0", "releases"],
    ["/projects/my-app/releases", "", "releases"],
    ["/projects/my-app/api-keys", "?release=1.3.0", "apiKeys"],
    ["/projects/my-app/shared-links", "", "sharedLinks"],
    ["/projects/my-app/environments", "", "environments"]
  ] as const)("classifies %s%s as %s", (pathname, search, expected) => {
    expect(getProjectPageSection(pathname, search)).toBe(expected);
  });

  it.each(["?viewRelease=", "?release=", "?amend="])(
    "does not treat an empty release query as release context: %s",
    search => {
      expect(getProjectPageSection("/projects/my-app", search)).toBe("parameters");
    }
  );

  it("does not classify project list, non-project, or unknown project routes", () => {
    expect(getProjectPageSection("/projects", "")).toBeUndefined();
    expect(getProjectPageSection("/users", "")).toBeUndefined();
    expect(getProjectPageSection("/projects/my-app/unknown", "")).toBeUndefined();
  });
});
