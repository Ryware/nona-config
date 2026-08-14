import { fireEvent, render, screen, within } from "@solidjs/testing-library";
import { vi } from "vitest";
import type { ConfigEntry } from "../../types";
import { ProjectParamsTable } from "./ProjectParamsTable";

vi.mock("@solid-primitives/media", () => ({
  createMediaQuery: () => () => false
}));

const longKey = "Layout:ExtremelyLongParameterIdentifierThatMustRemainInsideItsColumn";
const longDisplayName = "Extremely Long Parameter Display Name That Must Not Reach The Value";
const longValue = JSON.stringify({
  enabled: true,
  description: "A deliberately long value that must remain inside the flexible value column"
});

const entry: ConfigEntry = {
  project: "project",
  environment: "production",
  key: longKey,
  value: longValue,
  contentType: "json",
  scope: "all",
  activeVersion: 1,
  createdAt: "2026-08-14T00:00:00Z",
  updatedAt: "2026-08-14T00:00:00Z"
};

describe("ProjectParamsTable desktop containment", () => {
  it("uses fixed compact columns and preserves full truncated text and copying", () => {
    const onCopyValue = vi.fn<(key: string, value: string) => void>();

    render(() => (
      <ProjectParamsTable
        isLoading={false}
        projectId="project"
        activeEnvName="production"
        filteredConfig={[entry]}
        editingEntry={null}
        onSelectEntry={vi.fn()}
        onShareEntry={vi.fn()}
        onDeleteEntry={vi.fn()}
        canManage
        copiedKey={null}
        onCopyValue={onCopyValue}
        getParamMeta={() => ({ displayName: longDisplayName, description: "" })}
        initialDescription=""
        onCloseEntry={vi.fn()}
        onSaveSettings={vi.fn()}
        isSaving={false}
        historyVersions={[]}
        isHistoryLoading={false}
        isRollingBack={false}
        onRollbackVersion={vi.fn()}
        search=""
      />
    ));

    const table = screen.getByTestId("parameter-desktop-table");
    expect(table).toHaveClass("min-w-[48rem]", "table-fixed");
    expect(Array.from(table.querySelectorAll("col"), col => col.className)).toEqual([
      "w-[17rem]",
      "",
      "w-28",
      "w-28",
      "w-24"
    ]);

    const displayName = screen.getByTestId(`parameter-display-${longKey}`);
    expect(displayName).toHaveClass("block", "min-w-0", "truncate");
    expect(displayName).toHaveAttribute("title", longDisplayName);
    expect(displayName.parentElement).toHaveClass("min-w-0", "flex-1");
    expect(displayName.closest("td")).toHaveClass("min-w-0", "overflow-hidden", "px-4");

    const key = screen.getByTestId(`parameter-key-${longKey}`);
    expect(key).toHaveClass("block", "min-w-0", "truncate");
    expect(key).toHaveAttribute("title", longKey);

    const value = screen.getByTestId(`parameter-value-${longKey}`);
    expect(value).toHaveClass("min-w-0", "flex-1", "truncate");
    expect(value).toHaveAttribute("title", longValue);
    expect(value.parentElement).toHaveClass("w-full", "min-w-0");
    expect(value.closest("td")).toHaveClass("min-w-0", "overflow-hidden", "px-4");

    fireEvent.click(within(value.parentElement!).getByTitle("Copy value"));
    expect(onCopyValue).toHaveBeenCalledWith(longKey, longValue);
  });
});
