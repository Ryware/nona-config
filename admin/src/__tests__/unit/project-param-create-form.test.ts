import { describe, expect, it } from "vitest";
import { isValidConfigEntryValue } from "../../features/project-param-edit/ProjectParamCreateForm";

describe("isValidConfigEntryValue", () => {
  it("matches backend value validation for config entry content types", () => {
    expect(isValidConfigEntryValue("text", "hello")).toBe(true);
    expect(isValidConfigEntryValue("text", "")).toBe(false);

    expect(isValidConfigEntryValue("number", "42")).toBe(true);
    expect(isValidConfigEntryValue("number", "1.5")).toBe(true);
    expect(isValidConfigEntryValue("number", "abc")).toBe(false);
    expect(isValidConfigEntryValue("number", "")).toBe(false);

    expect(isValidConfigEntryValue("boolean", "true")).toBe(true);
    expect(isValidConfigEntryValue("boolean", "false")).toBe(true);
    expect(isValidConfigEntryValue("boolean", "")).toBe(false);
    expect(isValidConfigEntryValue("boolean", "yes")).toBe(false);

    expect(isValidConfigEntryValue("json", '{"enabled":true}')).toBe(true);
    expect(isValidConfigEntryValue("json", "{bad")).toBe(false);
  });
});
