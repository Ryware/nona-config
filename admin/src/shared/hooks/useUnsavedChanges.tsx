import { useBeforeLeave } from "@solidjs/router";
import {
  createContext,
  createMemo,
  createSignal,
  onCleanup,
  onMount,
  useContext,
  type Accessor,
  type ParentComponent
} from "solid-js";

import { ConfirmDialog } from "../ui/confirm-dialog";

export interface UnsavedChangesBlocker {
  id: string;
  isDirty: Accessor<boolean>;
  discard: () => void;
}

interface PendingAction {
  action: () => void;
  blockerIds: string[];
}

interface UnsavedChangesContextValue {
  registerBlocker: (blocker: UnsavedChangesBlocker) => () => void;
  requestAction: (action: () => void, blockerIds?: string[]) => void;
  isPromptOpen: Accessor<boolean>;
}

const UnsavedChangesContext = createContext<UnsavedChangesContextValue>();

export const UnsavedChangesProvider: ParentComponent = props => {
  const [blockers, setBlockers] = createSignal<UnsavedChangesBlocker[]>([]);
  const [pendingAction, setPendingAction] = createSignal<PendingAction | null>(null);
  const isPromptOpen = createMemo(() => pendingAction() !== null);
  const hasDirtyChanges = createMemo(() => blockers().some(blocker => blocker.isDirty()));

  const dirtyBlockers = (blockerIds?: string[]) => {
    const requestedIds = blockerIds ? new Set(blockerIds) : undefined;
    return blockers().filter(
      blocker => (!requestedIds || requestedIds.has(blocker.id)) && blocker.isDirty()
    );
  };

  const registerBlocker = (blocker: UnsavedChangesBlocker) => {
    setBlockers(current => [...current.filter(candidate => candidate.id !== blocker.id), blocker]);

    return () => {
      setBlockers(current => current.filter(candidate => candidate !== blocker));
    };
  };

  const requestAction = (action: () => void, blockerIds?: string[]) => {
    if (pendingAction()) return;

    const blockingDrafts = dirtyBlockers(blockerIds);
    if (blockingDrafts.length === 0) {
      action();
      return;
    }

    setPendingAction({
      action,
      blockerIds: blockingDrafts.map(blocker => blocker.id)
    });
  };

  const confirmDiscard = () => {
    const pending = pendingAction();
    if (!pending) return;

    const selectedIds = new Set(pending.blockerIds);
    for (const blocker of blockers()) {
      if (selectedIds.has(blocker.id)) {
        blocker.discard();
      }
    }

    setPendingAction(null);
    pending.action();
  };

  useBeforeLeave(event => {
    if (event.defaultPrevented || !hasDirtyChanges()) return;

    event.preventDefault();
    requestAction(() => event.retry(true));
  });

  onMount(() => {
    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      if (!hasDirtyChanges()) return;

      event.preventDefault();
      event.returnValue = "";
    };

    window.addEventListener("beforeunload", handleBeforeUnload);
    onCleanup(() => window.removeEventListener("beforeunload", handleBeforeUnload));
  });

  return (
    <UnsavedChangesContext.Provider value={{ registerBlocker, requestAction, isPromptOpen }}>
      {props.children}
      <ConfirmDialog
        open={isPromptOpen()}
        title="Discard Unsaved Changes?"
        message="You have unsaved parameter changes. Discard them and continue?"
        confirmLabel="Discard Changes"
        cancelLabel="Keep Editing"
        variant="warning"
        onConfirm={confirmDiscard}
        onCancel={() => setPendingAction(null)}
        testId="parameter-discard-dialog"
        confirmTestId="parameter-discard-confirm-button"
        cancelTestId="parameter-discard-cancel-button"
      />
    </UnsavedChangesContext.Provider>
  );
};

export function useUnsavedChanges() {
  const context = useContext(UnsavedChangesContext);
  if (!context) {
    throw new Error("useUnsavedChanges must be used within UnsavedChangesProvider");
  }

  return context;
}

export function useUnsavedChangesBlocker(blocker: UnsavedChangesBlocker) {
  const { registerBlocker } = useUnsavedChanges();

  onMount(() => {
    const unregister = registerBlocker(blocker);
    onCleanup(unregister);
  });
}
