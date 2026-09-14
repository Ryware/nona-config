import { describe, expect, it } from "vitest";

import { parseReleaseVersion, type ReleaseVersionFormat } from "./release-version";

describe("parseReleaseVersion", () => {
  it.each([
    ["0.0", "majorMinor", "0.0.0"],
    ["1.2", "majorMinor", "1.2.0"],
    ["01.002", "majorMinor", "1.2.0"],
    [" 1.2 ", "majorMinor", "1.2.0"],
    ["2147483647.2147483647", "majorMinor", "2147483647.2147483647.0"],
    ["0.0.0", "exact", "0.0.0"],
    ["01.002.0003", "exact", "1.2.3"],
    [" 1.2.3 ", "exact", "1.2.3"]
  ] satisfies Array<[string, ReleaseVersionFormat, string]>)(
    "normalizes %s in %s format",
    (value, format, expected) => {
      expect(parseReleaseVersion(value, format)).toBe(expected);
    }
  );

  it.each([
    ["", "majorMinor"],
    ["   ", "majorMinor"],
    ["1", "majorMinor"],
    ["1.2.0", "majorMinor"],
    ["1.2.x", "majorMinor"],
    ["1.-2", "majorMinor"],
    ["+1.2", "majorMinor"],
    ["1..2", "majorMinor"],
    ["１.2", "majorMinor"],
    ["2147483648.0", "majorMinor"],
    ["1.2", "exact"],
    ["1.2.x", "exact"],
    ["1.2.3.4", "exact"],
    ["1.2.2147483648", "exact"]
  ] satisfies Array<[string, ReleaseVersionFormat]>)("rejects %s in %s format", (value, format) => {
    expect(parseReleaseVersion(value, format)).toBeNull();
  });
});
