import { describe, expect, it } from "vitest";
import { localParamMetadataService } from "./metadata.service";

describe("localParamMetadataService", () => {
  it("preserves an explicitly cleared description", () => {
    localParamMetadataService.setMeta("project", "production", "KEY", {
      description: "Existing description",
    });
    localParamMetadataService.setMeta("project", "production", "KEY", {
      description: "",
    });

    expect(
      localParamMetadataService.getMeta("project", "production", "KEY").description,
    ).toBe("");
  });
});
