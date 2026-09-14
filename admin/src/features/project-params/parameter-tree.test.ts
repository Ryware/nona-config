import { describe, expect, it } from "vitest";
import type { ConfigEntry } from "../../types";
import { buildParameterTree, isTreeCompatibleKey, parameterName } from "./parameter-tree";

const entry = (key: string): ConfigEntry => ({
  project: "project",
  environment: "production",
  key,
  value: key,
  contentType: "text",
  scope: "all",
  activeVersion: 1,
  createdAt: "",
  updatedAt: ""
});

describe("parameter tree", () => {
  it("supports a terminal entry and children at the same path", () => {
    const tree = buildParameterTree([entry("A"), entry("A:B"), entry("A:C:D")]);

    expect(tree).toHaveLength(1);
    expect(tree[0].entry?.key).toBe("A");
    expect(tree[0].count).toBe(3);
    expect(tree[0].children.map(child => child.label)).toEqual(["B", "C"]);
    expect(tree[0].children[1].children[0].entry?.key).toBe("A:C:D");
  });

  it("renders invalid and deeper legacy keys safely at the root", () => {
    const tree = buildParameterTree([entry("A::B"), entry("A:B:C:D:E"), entry(":leading")]);

    expect(tree).toHaveLength(3);
    expect(tree.every(node => node.legacy && node.depth === 0 && node.entry)).toBe(true);
    expect(parameterName("A:B:C:D:E")).toBe("A:B:C:D:E");
  });

  it("accepts no more than four non-empty segments", () => {
    expect(isTreeCompatibleKey("A:B:C:D")).toBe(true);
    expect(isTreeCompatibleKey("A:B:C:D:E")).toBe(false);
    expect(isTreeCompatibleKey("A::B")).toBe(false);
    expect(parameterName("A:B:C:D")).toBe("D");
  });
});
