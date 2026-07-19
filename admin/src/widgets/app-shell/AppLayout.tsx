import { useLocation } from "@solidjs/router";
import { makePersisted } from "@solid-primitives/storage";
import { createEffect, createSignal, on, type JSX } from "solid-js";
import { Header } from "./Header";
import { Sidebar } from "./Sidebar";

export function AppLayout(props: { children?: JSX.Element }): JSX.Element {
  const location = useLocation();
  const [isSidebarOpen, setIsSidebarOpen] = createSignal(false);
  // eslint-disable-next-line solid/reactivity -- makePersisted intentionally wraps the signal.
  const [collapsed, setCollapsed] = makePersisted(createSignal(false), {
    deserialize: value => value === "true",
    name: "sidebar_collapsed",
    serialize: value => String(value)
  });

  const sidebarWidth = () => (collapsed() ? "lg:ml-16" : "lg:ml-64");

  const toggleCollapse = () => {
    setCollapsed(!collapsed());
  };

  // Close mobile sidebar on navigation
  createEffect(on(() => location.pathname, () => setIsSidebarOpen(false)));

  return (
    <div class="bg-background flex min-h-screen overflow-hidden">
      <Sidebar
        isOpen={isSidebarOpen()}
        onClose={() => setIsSidebarOpen(false)}
        collapsed={collapsed()}
        onToggleCollapse={toggleCollapse}
      />

      {/* Main Area */}
      <div
        class={`ml-0 flex min-w-0 flex-1 flex-col ${sidebarWidth()} transition-[margin-left] duration-300`}
      >
        <Header
          isSidebarOpen={isSidebarOpen()}
          onMenuToggle={() => setIsSidebarOpen(!isSidebarOpen())}
        />

        {/* Page content */}
        <main class="flex-1 overflow-auto p-6 md:p-8">{props.children}</main>
      </div>
    </div>
  );
}
