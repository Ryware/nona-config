import { createTimer } from "@solid-primitives/timer";
import { createContext, useContext, For, type ParentComponent } from "solid-js";
import { createStore } from "solid-js/store";

export type ToastType = "success" | "error" | "info" | "warning";

export interface Toast {
  id: string;
  type: ToastType;
  message: string;
  duration?: number;
}

interface ToastContextValue {
  addToast: (message: string, type?: ToastType, duration?: number) => void;
  removeToast: (id: string) => void;
}

const ToastContext = createContext<ToastContextValue>();

export const ToastProvider: ParentComponent = (props) => {
  const [state, setState] = createStore<{ toasts: Toast[] }>({ toasts: [] });

  const addToast = (
    message: string,
    type: ToastType = "info",
    duration = 3000,
  ) => {
    const id = Math.random().toString(36).substring(7);
    setState("toasts", (toasts) => [
      ...toasts,
      { id, message, type, duration },
    ]);
  };

  const removeToast = (id: string) => {
    setState("toasts", (toasts) => toasts.filter((t) => t.id !== id));
  };

  return (
    <ToastContext.Provider value={{ addToast, removeToast }}>
      {props.children}
      <ToastContainer toasts={state.toasts} removeToast={removeToast} />
    </ToastContext.Provider>
  );
};

export const useToast = () => {
  const context = useContext(ToastContext);
  if (!context) throw new Error("useToast must be used within ToastProvider");
  return context;
};

const toastStyles: Record<ToastType, string> = {
  success: "bg-success/10 border border-success/25 text-success",
  error: "bg-error/10 border border-error/25 text-error",
  warning: "bg-warning/10 border border-warning/25 text-warning",
  info: "bg-surface-container-high border border-primary/20 text-primary",
};

const toastIcons: Record<ToastType, string> = {
  success: "check_circle",
  error: "error",
  warning: "warning",
  info: "info",
};

const ToastContainer: ParentComponent<{
  toasts: Toast[];
  removeToast: (id: string) => void;
}> = (props) => {
  return (
    <div class="fixed bottom-5 right-5 z-50 flex flex-col gap-2" role="status" aria-live="polite">
      <For each={props.toasts}>
        {(toast) => <ToastItem toast={toast} removeToast={props.removeToast} />}
      </For>
    </div>
  );
};

const ToastItem = (props: {
  toast: Toast;
  removeToast: (id: string) => void;
}) => {
  createTimer(
    () => props.removeToast(props.toast.id),
    () => (props.toast.duration && props.toast.duration > 0 ? props.toast.duration : false),
    setTimeout
  );

  return (
    <div
      class={`flex items-center gap-3 rounded-xl px-4 py-3 shadow-xl max-w-sm ${toastStyles[props.toast.type]} animate-in`}
    >
      <span class="material-symbols-outlined text-[18px] shrink-0">
        {toastIcons[props.toast.type]}
      </span>
      <span class="text-[13px] flex-1">{props.toast.message}</span>
      <button
        onClick={() => props.removeToast(props.toast.id)}
        aria-label="Dismiss notification"
        class="shrink-0 text-current opacity-60 hover:opacity-100 transition-opacity bg-transparent border-0 cursor-pointer"
      >
        <span class="material-symbols-outlined text-[16px]">close</span>
      </button>
    </div>
  );
};
