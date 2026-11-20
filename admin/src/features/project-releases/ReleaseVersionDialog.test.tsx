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

async function submitVersion(value: string) {
  fireEvent.input(screen.getByTestId("release-version-input"), { target: { value } });
  await Promise.resolve();
  fireEvent.click(screen.getByTestId("release-version-confirm-button"));
}

describe("ReleaseVersionDialog", () => {
  it.each(["1.2.0", "1.-2", "2147483648.0"])("rejects invalid version %s", async value => {
    const onConfirm = renderDialog();

    await submitVersion(value);
    await Promise.resolve();

    expect(screen.getByText("Use major.minor.")).toBeInTheDocument();
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it("submits the canonical exact version", async () => {
    const onConfirm = renderDialog();

    await submitVersion(" 01.002 ");
    await Promise.resolve();

    expect(onConfirm).toHaveBeenCalledWith("1.2.0");
  });

  it("rejects a canonically equivalent existing version", async () => {
    const onConfirm = renderDialog({ existingVersions: ["1.2.0"] });

    await submitVersion("01.002");
    await Promise.resolve();

    expect(screen.getByText("That version already exists.")).toBeInTheDocument();
    expect(onConfirm).not.toHaveBeenCalled();
  });
});
