import { describe, expect, it } from "vitest";
import { getConfigEntryValueError, validateConfigEntryKey } from "./config-entry-value";

describe("parameter draft validation", () => {
  it("rejects empty path segments and keys deeper than four segments", () => {
    expect(validateConfigEntryKey("A::B")).toMatch(/empty segments/i);
    expect(validateConfigEntryKey(":A")).toMatch(/empty segments/i);
    expect(validateConfigEntryKey("A:B:")).toMatch(/empty segments/i);
    expect(validateConfigEntryKey("A:B:C:D:E")).toMatch(/at most 4 segments/i);
    expect(validateConfigEntryKey("A:B:C:D")).toBeUndefined();
  });

  it("returns the native JSON parser detail", () => {
    const error = getConfigEntryValueError("json", '{"enabled":');
    expect(error).toMatch(/^Invalid JSON:/);
    expect(error).not.toBe("Invalid JSON value.");
  });
});
