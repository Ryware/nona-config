import {
  type ParentComponent,
  createContext,
  createSignal,
  Show,
  useContext,
} from "solid-js";

interface TabsContextValue {
  activeTab: () => string;
  setActiveTab: (value: string) => void;
}

const TabsContext = createContext<TabsContextValue>();

interface TabsProps {
  defaultValue: string;
  class?: string;
}

export const Tabs: ParentComponent<TabsProps> = (props) => {
  const [activeTab, setActiveTab] = createSignal(props.defaultValue);

  return (
    <TabsContext.Provider value={{ activeTab, setActiveTab }}>
      <div class={props.class}>{props.children}</div>
    </TabsContext.Provider>
  );
};

interface TabsListProps {
  class?: string;
}

export const TabsList: ParentComponent<TabsListProps> = (props) => {
  return (
    <div
      role="tablist"
      class={`flex gap-1 border-b border-outline-variant/10 ${props.class || ""}`}
    >
      {props.children}
    </div>
  );
};

interface TabsTriggerProps {
  value: string;
  class?: string;
}

export const TabsTrigger: ParentComponent<TabsTriggerProps> = (props) => {
  const context = useContext(TabsContext);
  if (!context) throw new Error("TabsTrigger must be used within Tabs");

  const isActive = () => context.activeTab() === props.value;

  return (
    <button
      role="tab"
      aria-selected={isActive()}
      class={`px-4 py-2.5 text-[13px] font-medium transition-all border-b-2 -mb-px focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 ${
        isActive()
          ? "text-primary border-primary font-bold"
          : "text-outline border-transparent hover:text-on-surface-variant hover:border-outline-variant/30"
      } ${props.class || ""}`}
      onClick={() => context.setActiveTab(props.value)}
    >
      {props.children}
    </button>
  );
};

interface TabsContentProps {
  value: string;
  class?: string;
}

export const TabsContent: ParentComponent<TabsContentProps> = (props) => {
  const context = useContext(TabsContext);
  if (!context) throw new Error("TabsContent must be used within Tabs");

  return (
    <Show when={context.activeTab() === props.value}>
      <div role="tabpanel" class={props.class}>
        {props.children}
      </div>
    </Show>
  );
};
