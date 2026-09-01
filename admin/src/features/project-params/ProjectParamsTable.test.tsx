import { fireEvent, render, screen, waitFor } from "@solidjs/testing-library";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { createSignal } from "solid-js";
import type { ConfigEntry } from "../../types";
import { ProjectParamsTable, type ProjectParamsTableProps } from "./ProjectParamsTable";

vi.mock("@solid-primitives/media", () => ({
  createMediaQuery: () => () => false
}));

const entry = (patch: Partial<ConfigEntry> = {}): ConfigEntry => ({
  project: "project",
  environment: "production",
  key: "Checkout:FreeShippingThreshold",
  value: "25",
  contentType: "number",
  scope: "client",
  unit: "USD",
  activeVersion: 1,
  createdAt: "2026-08-14T00:00:00Z",
  updatedAt: "2026-08-14T00:00:00Z",
  ...patch
});

const tableProps = (patch: Partial<ProjectParamsTableProps> = {}): ProjectParamsTableProps => ({
  isLoading: false,
  projectId: "project",
  activeEnvName: "production",
  filteredConfig: [entry()],
  onSelectEntry: vi.fn(),
  onDeleteEntry: vi.fn(),
  onUpdateValue: vi.fn(),
  canManage: true,
  search: "",
  ...patch
});

describe("ProjectParamsTable", () => {
  beforeEach(() => localStorage.clear());

  it("groups colon-delimited keys, expands initially, and persists collapse state", () => {
    render(() => <ProjectParamsTable {...tableProps()} />);

    const group = screen.getByTestId("parameter-group-Checkout");
    expect(group).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByTestId("parameter-row-Checkout:FreeShippingThreshold")).toBeInTheDocument();

    fireEvent.click(group);
    expect(group).toHaveAttribute("aria-expanded", "false");
    expect(screen.queryByTestId("parameter-row-Checkout:FreeShippingThreshold")).not.toBeInTheDocument();
    expect(JSON.parse(localStorage.getItem("nona_parameter_tree:project:production") ?? "[]"))
      .toContain("Checkout");
  });

  it("temporarily reveals a matching collapsed path without changing saved state", () => {
    const [search, setSearch] = createSignal("");
    render(() => <ProjectParamsTable {...tableProps()} search={search()} />);
    fireEvent.click(screen.getByTestId("parameter-group-Checkout"));
    expect(screen.queryByTestId("parameter-row-Checkout:FreeShippingThreshold")).not.toBeInTheDocument();

    setSearch("shipping");
    expect(screen.getByTestId("parameter-row-Checkout:FreeShippingThreshold")).toBeInTheDocument();
    expect(JSON.parse(localStorage.getItem("nona_parameter_tree:project:production") ?? "[]"))
      .toContain("Checkout");

    setSearch("");
    expect(screen.queryByTestId("parameter-row-Checkout:FreeShippingThreshold")).not.toBeInTheDocument();
  });

  it("keeps invalid JSON drafts, reports the parser reason, and only updates valid changes", async () => {
    const onUpdateValue = vi.fn(() => Promise.resolve());
    render(() => (
      <ProjectParamsTable
        {...tableProps({
          filteredConfig: [entry({ key: "Settings", value: '{"enabled":true}', contentType: "json", unit: null })],
          onUpdateValue
        })}
      />
    ));

    const input = screen.getByTestId("parameter-value-input-Settings");
    const update = screen.getByTestId("parameter-update-Settings");
    expect(update).toHaveTextContent("Update");
    fireEvent.input(input, { target: { value: '{"enabled":' } });

    expect(input).toHaveValue('{"enabled":');
    expect(screen.getByRole("alert")).toHaveTextContent(/invalid json:/i);
    expect(update).toBeDisabled();
    expect(onUpdateValue).not.toHaveBeenCalled();

    fireEvent.input(input, { target: { value: '{"enabled":false}' } });
    expect(update).toBeEnabled();
    fireEvent.click(update);
    await waitFor(() => expect(onUpdateValue).toHaveBeenCalledWith(
      expect.objectContaining({ key: "Settings" }),
      '{"enabled":false}'
    ));
  });

  it("preserves a newer draft when an earlier update response arrives", async () => {
    let resolveUpdate: (() => void) | undefined;
    const pendingUpdate = new Promise<void>(resolve => {
      resolveUpdate = resolve;
    });
    const [entries, setEntries] = createSignal([entry()]);
    const onUpdateValue = vi.fn(async (current: ConfigEntry, value: string) => {
      await pendingUpdate;
      setEntries([{ ...current, value, activeVersion: current.activeVersion + 1 }]);
    });

    render(() => (
      <ProjectParamsTable
        {...tableProps()}
        filteredConfig={entries()}
        onUpdateValue={onUpdateValue}
      />
    ));

    const input = screen.getByTestId(`parameter-value-input-${entry().key}`);
    const update = screen.getByTestId(`parameter-update-${entry().key}`);
    fireEvent.input(input, { target: { value: "30" } });
    fireEvent.click(update);
    await waitFor(() => expect(onUpdateValue).toHaveBeenCalledWith(
      expect.objectContaining({ key: entry().key }),
      "30"
    ));

    fireEvent.input(input, { target: { value: "35" } });
    resolveUpdate?.();

    await waitFor(() => expect(
      screen.getByTestId(`parameter-update-${entry().key}`)
    ).toBeEnabled());
    expect(screen.getByTestId(`parameter-value-input-${entry().key}`)).toHaveValue("35");
  });

  it("keeps a controlled draft across row remounts and uses the supplied action label", () => {
    const [draftValues, setDraftValues] = createSignal<Record<string, string>>({});
    const onDraftValueChange = vi.fn((current: ConfigEntry, value: string) => {
      setDraftValues(previous => ({ ...previous, [current.key]: value }));
    });
    const props = () => ({
      ...tableProps(),
      draftValues: draftValues(),
      onDraftValueChange,
      updateLabel: "Apply to draft"
    }) as ProjectParamsTableProps;

    render(() => <ProjectParamsTable {...props()} />);

    const inputId = `parameter-value-input-${entry().key}`;
    fireEvent.input(screen.getByTestId(inputId), { target: { value: "30" } });
    expect(onDraftValueChange).toHaveBeenCalledWith(
      expect.objectContaining({ key: entry().key }),
      "30"
    );
    expect(screen.getByTestId(`parameter-update-${entry().key}`)).toHaveTextContent("Apply to draft");

    fireEvent.click(screen.getByTestId("parameter-group-Checkout"));
    expect(screen.queryByTestId(inputId)).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId("parameter-group-Checkout"));
    expect(screen.getByTestId(inputId)).toHaveValue("30");
  });

  it("uses an accessible boolean switch and shows number units", () => {
    render(() => (
      <ProjectParamsTable
        {...tableProps({
          filteredConfig: [
            entry({ key: "Features:Checkout", value: "true", contentType: "boolean", unit: null }),
            entry()
          ]
        })}
      />
    ));

    const toggle = screen.getByRole("switch", { name: "Value for Features:Checkout" });
    expect(toggle).toHaveAttribute("aria-checked", "true");
    fireEvent.click(toggle);
    expect(toggle).toHaveAttribute("aria-checked", "false");
    expect(screen.getByLabelText("Unit USD")).toBeInTheDocument();
  });

  it("opens details from the labelled edit action and removes badges", () => {
    const onSelectEntry = vi.fn();
    render(() => <ProjectParamsTable {...tableProps({ onSelectEntry })} />);

    const edit = screen.getByTestId("parameter-edit-Checkout:FreeShippingThreshold");
    fireEvent.click(edit);
    expect(onSelectEntry).toHaveBeenCalledWith(expect.objectContaining({ key: entry().key }), edit);
    expect(screen.queryByText("active v1")).not.toBeInTheDocument();
  });
});
