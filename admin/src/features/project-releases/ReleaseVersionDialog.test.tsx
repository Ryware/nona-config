import { fireEvent, render, screen } from "@solidjs/testing-library";
import { describe, expect, it, vi } from "vitest";

import { ReleaseVersionDialog } from "./ReleaseVersionDialog";

interface RenderDialogOptions {
  existingVersions?: string[];
}

function renderDialog({ existingVersions = [] }: RenderDialogOptions = {}) {
  const onConfirm = vi.fn<(version: string) => void>();
  render(() => (
    <ReleaseVersionDialog
      open
      title="Create a version"
      initialVersion=""
      existingVersions={existingVersions}
      confirmLabel="Continue"
      validationMessage="Use major.minor."
      versionFormat="majorMinor"
      onConfirm={onConfirm}
      onCancel={vi.fn()}
    />
  ));
  return onConfirm;
}

function submitVersion(value: string) {
  fireEvent.input(screen.getByTestId("release-version-input"), { target: { value } });
  fireEvent.click(screen.getByTestId("release-version-confirm-button"));
}

describe("ReleaseVersionDialog", () => {
  it.each(["1.2.0", "1.-2", "2147483648.0"])("rejects invalid version %s", value => {
    const onConfirm = renderDialog();

    submitVersion(value);

    expect(screen.getByText("Use major.minor.")).toBeInTheDocument();
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it("submits the canonical exact version", () => {
    const onConfirm = renderDialog();

    submitVersion(" 01.002 ");

    expect(onConfirm).toHaveBeenCalledWith("1.2.0");
  });

  it("rejects a canonically equivalent existing version", () => {
    const onConfirm = renderDialog({ existingVersions: ["1.2.0"] });

    submitVersion("01.002");

    expect(screen.getByText("That version already exists.")).toBeInTheDocument();
    expect(onConfirm).not.toHaveBeenCalled();
  });
});
