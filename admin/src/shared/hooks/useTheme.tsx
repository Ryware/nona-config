import { createUserTheme } from "@solid-primitives/cookies";
import {
  createContext,
  createEffect,
  type Accessor,
  type ParentProps,
  useContext,
} from "solid-js";

export type Theme = "dark" | "light";

const THEME_STORAGE_KEY = "nona_theme";
const DEFAULT_THEME: Theme = "light";

interface ThemeContextValue {
  theme: Accessor<Theme>;
  setTheme: (theme: Theme) => void;
  toggleTheme: () => void;
}

const ThemeContext = createContext<ThemeContextValue>();

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
  const [theme, setTheme] = createUserTheme(THEME_STORAGE_KEY, {
    defaultValue: DEFAULT_THEME
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
