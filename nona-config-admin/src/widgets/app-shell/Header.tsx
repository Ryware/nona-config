import { ThemeToggle } from "../../shared/ui/ThemeToggle";
import { Breadcrumbs } from "./Breadcrumbs";

interface HeaderProps {
  onMenuToggle: () => void;
  onSearchClick: () => void;
  isSidebarOpen: boolean;
}

export function Header(props: HeaderProps) {
  return (
    <header class="sticky top-0 z-40 w-full bg-background/80 backdrop-blur-md border-b border-outline-variant/15 flex items-center h-14 px-5 md:px-7 gap-3 shrink-0">
      {/* Mobile hamburger menu */}
      <button
        onClick={() => props.onMenuToggle()}
        class="lg:hidden p-2 -ml-1 text-on-surface-variant hover:text-on-surface bg-transparent border-0 cursor-pointer flex items-center justify-center rounded-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
        aria-label="Toggle navigation menu"
      >
        <span class="material-symbols-outlined text-2xl">
          {props.isSidebarOpen ? "close" : "menu"}
        </span>
      </button>

      {/* Breadcrumbs navigation */}
      <Breadcrumbs />

      {/* Action actions */}
      <div class="flex items-center gap-2 shrink-0">
        <button
          onClick={() => props.onSearchClick()}
          class="flex items-center gap-2 h-8 px-3 rounded-xl bg-surface-container-low border border-outline-variant/20 text-outline hover:text-on-surface hover:border-outline-variant/40 transition-all cursor-pointer"
          title="Search (⌘K)"
          aria-label="Open search"
        >
          <span class="material-symbols-outlined text-[16px]">search</span>
          <span class="hidden md:inline text-[11px] font-medium">Search…</span>
          <kbd class="hidden lg:flex items-center gap-0.5 text-[9px] font-bold text-outline/60 bg-surface-container-lowest border border-outline-variant/20 rounded px-1">
            ⌘K
          </kbd>
        </button>

        <ThemeToggle />

        <div class="w-px h-5 bg-outline-variant/20" />

        <a
          class="hover:text-primary transition-colors flex items-center gap-1 text-[11px] font-medium text-outline"
          href="https://www.nonaconfig.com/support"
          target="_blank"
          rel="noopener noreferrer"
        >
          <span class="material-symbols-outlined text-[15px]">contact_support</span>
          <span class="hidden md:inline">Support</span>
        </a>
        <a
          class="hover:text-primary transition-colors flex items-center gap-1 text-[11px] font-medium text-outline"
          href="https://www.nonaconfig.com/docs"
          target="_blank"
          rel="noopener noreferrer"
        >
          <span class="material-symbols-outlined text-[15px]">terminal</span>
          <span class="hidden md:inline">API Docs</span>
        </a>
      </div>
    </header>
  );
}
