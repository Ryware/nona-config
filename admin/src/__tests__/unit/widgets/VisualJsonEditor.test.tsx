import { render, screen, fireEvent } from "@solidjs/testing-library";
import { createSignal } from "solid-js";
import { describe, expect, it } from "vitest";
import { VisualJsonEditor } from "../../../shared/ui/visual-json-editor";

describe("VisualJsonEditor", () => {
  it("keeps a newly added blank row visible in controlled mode", async () => {
    render(() => {
      const [value, setValue] = createSignal('{"enabled":true}');
      return <VisualJsonEditor value={value()} onChange={setValue} />;
    });

    fireEvent.click(await screen.findByTestId("visual-json-editor-add-item"));

    expect(screen.getAllByPlaceholderText("Key")).toHaveLength(2);
  });

  it("allows typing into the key input for a newly added row", async () => {
    render(() => {
      const [value, setValue] = createSignal('{"enabled":true}');
      return <VisualJsonEditor value={value()} onChange={setValue} />;
    });

    fireEvent.click(await screen.findByTestId("visual-json-editor-add-item"));

    const keyInputs = screen.getAllByPlaceholderText("Key") as HTMLInputElement[];
    keyInputs[1].focus();
    fireEvent.input(keyInputs[1], { target: { value: "featureFlag" } });
    fireEvent.input(keyInputs[1], { target: { value: "featureFlagTwo" } });

    expect(keyInputs[1]).toHaveValue("featureFlagTwo");
    expect(document.activeElement).toBe(keyInputs[1]);
  });
});
