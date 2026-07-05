import { makePersisted } from "@solid-primitives/storage";
import {
  createContext,
  createEffect,
  createSignal,
  type Accessor,
  type ParentProps,
  useContext,
} from "solid-js";

export type Theme = "dark" | "light";

const THEME_STORAGE_KEY = "nona_theme";
const DEFAULT_THEME: Theme = "dark";

interface ThemeContextValue {
  theme: Accessor<Theme>;
  setTheme: (theme: Theme) => void;
  toggleTheme: () => void;
}

const ThemeContext = createContext<ThemeContextValue>();

const isTheme = (value: string): value is Theme =>
  value === "dark" || value === "light";

const applyTheme = (theme: Theme): void => {
  if (typeof document === "undefined") {
    return;
  }

  // TODO: Change it to Proxy <Document/> instead.
  const root = document.documentElement;
  root.dataset.theme = theme;
  root.classList.toggle("dark", theme === "dark");
  root.style.colorScheme = theme;
};

export function ThemeProvider(props: ParentProps) {
  // eslint-disable-next-line solid/reactivity -- makePersisted intentionally wraps the signal.
  const [theme, setTheme] = makePersisted(createSignal<Theme>(DEFAULT_THEME), {
    deserialize: value => (isTheme(value) ? value : DEFAULT_THEME),
    name: THEME_STORAGE_KEY,
    serialize: value => value,
  });

  createEffect(() => {
    applyTheme(theme());
  });

  const toggleTheme = (): void => {
    setTheme(theme() === "light" ? "dark" : "light");
  };

  return (
    <ThemeContext.Provider value={{ theme, setTheme, toggleTheme }}>
      {props.children}
    </ThemeContext.Provider>
  );
}

export function useTheme() {
  const context = useContext(ThemeContext);

  if (!context) {
    throw new Error("useTheme must be used within ThemeProvider");
  }

  return context;
}
