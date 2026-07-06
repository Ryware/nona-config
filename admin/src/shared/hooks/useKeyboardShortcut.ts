import { createShortcut } from "@solid-primitives/keyboard";

interface ShortcutOptions {
  metaOrCtrl?: boolean;
}

const isEditableElement = (element: Element | null): boolean =>
  !!element &&
  (element.tagName === "INPUT" ||
    element.tagName === "TEXTAREA" ||
    element.getAttribute("contenteditable") === "true");

export function useKeyboardShortcut(
  key: string,
  callback: (e: KeyboardEvent) => void,
  options?: ShortcutOptions
) {
  const handleShortcut = (event: KeyboardEvent | null) => {
    if (!event) return;

    if (isEditableElement(document.activeElement)) {
      return;
    }

    event.preventDefault();
    callback(event);
  };

  if (options?.metaOrCtrl ?? true) {
    createShortcut(["Control", key], handleShortcut, { preventDefault: false, requireReset: true });
    createShortcut(["Meta", key], handleShortcut, { preventDefault: false, requireReset: true });
    return;
  }

  createShortcut([key], handleShortcut, { preventDefault: false, requireReset: true });
}
