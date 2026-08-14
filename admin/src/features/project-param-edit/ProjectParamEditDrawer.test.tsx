import { writeClipboard } from "@solid-primitives/clipboard";
import { fireEvent, render, screen, waitFor, within } from "@solidjs/testing-library";
import { describe, expect, it, vi } from "vitest";
import type { ConfigEntry, ConfigEntryVersion } from "../../types";
import { ProjectParamEditDrawer } from "./ProjectParamEditDrawer";

vi.mock("@solid-primitives/clipboard", () => ({
  writeClipboard: vi.fn(() => Promise.resolve())
}));

const longJson = JSON.stringify({
  enabled: true,
  name: "a parameter value that is intentionally much wider than the history value column",
  nested: { retries: 3, regions: ["eu-west", "us-east", "ap-south"] }
});

const entry: ConfigEntry = {
  project: "test-project",
  environment: "production",
  key: "MIXED_VALUE",
  value: longJson,
  contentType: "json",
  scope: "server",
  activeVersion: 5,
  createdAt: "2026-08-14T08:00:00Z",
  updatedAt: "2026-08-14T12:00:00Z"
};

const versions: ConfigEntryVersion[] = [
  { ...entry, version: 5, actor: "owner@example.com", createdAt: "2026-08-14T12:00:00Z" },
  {
    ...entry,
    version: 4,
    value: "true",
    contentType: "boolean",
    scope: "client",
    actor: "owner@example.com",
    createdAt: "2026-08-14T11:00:00Z"
  },
  {
    ...entry,
    version: 3,
    value: "999999999999999999",
    contentType: "number",
    scope: "all",
    actor: "owner@example.com",
    createdAt: "2026-08-14T10:00:00Z"
  },
  {
    ...entry,
    version: 2,
    value: "short text",
    contentType: "text",
    scope: "server",
    actor: "owner@example.com",
    createdAt: "2026-08-14T09:00:00Z"
  }
];

interface RenderHistoryOptions {
  historyLayout?: "mobile" | "desktop";
  canManage?: boolean;
  onRollbackVersion?: ReturnType<typeof createRollbackMock>;
}

const createRollbackMock = () => vi.fn<(version: ConfigEntryVersion) => void>();

function renderHistory({
  historyLayout = "desktop",
  canManage = true,
  onRollbackVersion = createRollbackMock()
}: RenderHistoryOptions = {}) {
  render(() => (
    <ProjectParamEditDrawer
      entry={entry}
      activeEnvName="production"
      initialDescription="Mixed history values"
      onClose={vi.fn()}
      onSaveSettings={vi.fn()}
      isSaving={false}
      canManage={canManage}
      historyVersions={versions}
      isHistoryLoading={false}
      isRollingBack={false}
      onRollbackVersion={onRollbackVersion}
      historyLayout={historyLayout}
    />
  ));
  fireEvent.click(screen.getByRole("button", { name: /history/i }));
  return onRollbackVersion;
}

describe("ProjectParamEditDrawer history", () => {
  it("renders one shared header and no per-field status labels", () => {
    renderHistory();

    const header = screen.getByTestId("parameter-history-header");
    expect(header).toHaveClass("grid-cols-[minmax(0,1fr)_6rem_5rem_8rem]");
    expect(within(header).getByText("Value")).toBeInTheDocument();
    expect(within(header).getByText("Datatype")).toBeInTheDocument();
    expect(within(header).getByText("Scope")).toBeInTheDocument();
    expect(within(header).getByText("Rollback")).toBeInTheDocument();
    expect(screen.getAllByText("Datatype")).toHaveLength(1);
    expect(screen.getAllByText("Scope")).toHaveLength(1);
    expect(screen.queryByText("changed")).not.toBeInTheDocument();
    expect(screen.queryByText("initial")).not.toBeInTheDocument();

    for (const version of versions) {
      expect(
        screen.getByTestId(`parameter-history-desktop-fields-v${version.version}`)
      ).toHaveClass("grid-cols-[minmax(0,1fr)_6rem_5rem_8rem]");
    }
  });

  it("retains full long values as hover text and rolls back the selected revision", () => {
    const onRollbackVersion = renderHistory();

    expect(screen.getByTitle(longJson)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Rollback to v4" }));
    expect(onRollbackVersion).toHaveBeenCalledWith(versions[1]);
  });

  it("stacks the actor below the version and keeps the active badge beside it", () => {
    renderHistory();

    const identity = screen.getByTestId("parameter-history-desktop-identity-v5");
    expect(within(identity).getByText("v5")).toBeInTheDocument();
    expect(within(identity).getByText("active")).toBeInTheDocument();
    expect(within(identity).getByText("owner@example.com")).toBeInTheDocument();
  });

  it("reserves desktop space after the truncated value field and copies the complete value", async () => {
    renderHistory();

    const valueField = screen.getByTestId("parameter-history-value-v5");
    expect(valueField).toHaveClass("w-full", "rounded-lg");
    expect(valueField.parentElement).toHaveClass("min-w-0", "pr-12");
    expect(valueField).not.toHaveClass("md:w-1/2");
    expect(within(valueField).getByTitle(longJson)).toHaveClass("truncate");

    fireEvent.click(screen.getByRole("button", { name: "Copy value from v5" }));
    await waitFor(() => expect(writeClipboard).toHaveBeenCalledWith(longJson));
  });

  it("opens the active mobile revision first and keeps only one dropdown open", () => {
    renderHistory({ historyLayout: "mobile" });

    const activeTrigger = screen.getByRole("button", { name: "Version v5 details" });
    const previousTrigger = screen.getByRole("button", { name: "Version v4 details" });

    expect(screen.queryByTestId("parameter-history-header")).not.toBeInTheDocument();
    expect(activeTrigger).toHaveAttribute("aria-expanded", "true");
    expect(previousTrigger).toHaveAttribute("aria-expanded", "false");
    expect(screen.getByTestId("parameter-history-mobile-panel-v5")).toHaveAttribute(
      "aria-labelledby",
      "parameter-history-trigger-v5"
    );

    fireEvent.click(previousTrigger);

    expect(activeTrigger).toHaveAttribute("aria-expanded", "false");
    expect(previousTrigger).toHaveAttribute("aria-expanded", "true");
    expect(screen.queryByTestId("parameter-history-mobile-panel-v5")).not.toBeInTheDocument();

    const previousPanel = screen.getByTestId("parameter-history-mobile-panel-v4");
    expect(within(previousPanel).getByText("Value")).toBeInTheDocument();
    expect(within(previousPanel).getByText("Datatype")).toBeInTheDocument();
    expect(within(previousPanel).getByText("Scope")).toBeInTheDocument();
    expect(within(previousPanel).getByText("true")).toBeInTheDocument();
    expect(within(previousPanel).getByText("boolean")).toBeInTheDocument();
    expect(within(previousPanel).getByText("client")).toBeInTheDocument();

    fireEvent.click(previousTrigger);
    expect(previousTrigger).toHaveAttribute("aria-expanded", "false");
    expect(screen.queryByTestId("parameter-history-mobile-panel-v4")).not.toBeInTheDocument();
  });

  it("truncates mobile values in a copyable field and rolls back from the details", async () => {
    const onRollbackVersion = renderHistory({ historyLayout: "mobile" });

    const activePanel = screen.getByTestId("parameter-history-mobile-panel-v5");
    const activeValue = screen.getByTestId("parameter-history-value-v5");
    expect(within(activeValue).getByTitle(longJson)).toHaveClass("truncate");
    fireEvent.click(within(activeValue).getByRole("button", { name: "Copy value from v5" }));
    await waitFor(() => expect(writeClipboard).toHaveBeenCalledWith(longJson));
    expect(
      within(activePanel).queryByRole("button", { name: /rollback/i })
    ).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Version v4 details" }));
    fireEvent.click(screen.getByRole("button", { name: "Rollback to v4" }));
    expect(onRollbackVersion).toHaveBeenCalledWith(versions[1]);
  });

  it("hides mobile rollback actions when the project is not manageable", () => {
    renderHistory({ historyLayout: "mobile", canManage: false });

    fireEvent.click(screen.getByRole("button", { name: "Version v4 details" }));
    expect(screen.queryByRole("button", { name: /rollback to/i })).not.toBeInTheDocument();
  });
});
