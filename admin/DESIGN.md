---
name: Nona Config Admin
description: Configuration management for non-technical administrators
colors:
  obsidian: "#09090b"
  obsidian-raised: "#121214"
  obsidian-surface: "#18181b"
  obsidian-muted: "#27272a"
  obsidian-bright: "#3f3f46"
  zinc-text: "#f4f4f5"
  zinc-secondary: "#a1a1aa"
  zinc-subtle: "#71717a"
  indigo-primary: "#6366f1"
  indigo-container: "#818cf8"
  indigo-deep: "#4f46e5"
  emerald-accent: "#10b981"
  emerald-container: "#064e3b"
  coral-error: "#f87171"
  coral-container: "#7f1d1d"
  amber-warning: "#f59e0b"
  mint-success: "#34d399"
typography:
  headline:
    fontFamily: "Space Grotesk, sans-serif"
    fontWeight: 700
    fontSize: "17px"
    lineHeight: 1.3
    letterSpacing: "-0.01em"
  body:
    fontFamily: "Inter, system-ui, -apple-system, BlinkMacSystemFont, Segoe UI, sans-serif"
    fontWeight: 400
    fontSize: "13px"
    lineHeight: 1.5
  label:
    fontFamily: "Inter, system-ui, sans-serif"
    fontWeight: 500
    fontSize: "11px"
    lineHeight: 1
    letterSpacing: "0.05em"
  mono:
    fontFamily: "JetBrains Mono, ui-monospace, SFMono-Regular, monospace"
    fontWeight: 400
    fontSize: "11px"
    lineHeight: 1.5
rounded:
  sm: "6px"
  md: "8px"
  lg: "12px"
  xl: "16px"
  full: "9999px"
spacing:
  xs: "4px"
  sm: "8px"
  md: "16px"
  lg: "24px"
  xl: "32px"
  page: "32px"
components:
  button-primary:
    backgroundColor: "{colors.indigo-primary}"
    textColor: "#ffffff"
    rounded: "{rounded.lg}"
    padding: "0 24px"
    height: "44px"
  button-primary-hover:
    backgroundColor: "{colors.indigo-primary}"
  button-secondary:
    backgroundColor: "{colors.obsidian-muted}"
    textColor: "{colors.zinc-secondary}"
    rounded: "{rounded.lg}"
    padding: "0 24px"
    height: "44px"
  button-ghost:
    backgroundColor: "transparent"
    textColor: "{colors.zinc-subtle}"
    rounded: "{rounded.lg}"
    padding: "0 12px"
    height: "36px"
  button-destructive:
    backgroundColor: "{colors.coral-error}"
    textColor: "#ffffff"
    rounded: "{rounded.lg}"
    padding: "0 24px"
    height: "44px"
  input-default:
    backgroundColor: "#030303"
    textColor: "{colors.zinc-text}"
    rounded: "{rounded.lg}"
    padding: "0 14px"
    height: "44px"
  card-default:
    backgroundColor: "{colors.obsidian-raised}"
    textColor: "{colors.zinc-text}"
    rounded: "{rounded.xl}"
    padding: "20px"
  chip-default:
    backgroundColor: "{colors.obsidian-muted}"
    textColor: "{colors.zinc-secondary}"
    rounded: "{rounded.md}"
    padding: "2px 8px"
---

# Design System: Nona Config Admin

## 1. Overview

**Creative North Star: "The Obsidian Console"**

A precise instrument carved from dark stone. The interface exists to serve a single purpose: giving non-technical administrators confident control over configuration without technical anxiety. Every surface is a tonal layer of near-black zinc, punctuated only by controlled bursts of electric indigo for active state and emerald for confirmation. The density is medium: enough breathing room for clarity, enough information on screen for efficiency.

The system explicitly rejects confusing developer-heavy dashboards with cryptic acronyms; generic SaaS templates with loud gradients, unnecessary animations, and low-contrast text; and interfaces that lack inline validation, explanations, or safety nets for configuration changes (per PRODUCT.md anti-references). It draws inspiration from Linear and Vercel's disciplined dark surfaces but adapts everything toward non-technical operators who need plain language, not code.

**Key Characteristics:**
- Dark zinc tonal hierarchy with 6 surface steps for layering without shadows
- Single accent (indigo #6366f1) reserved for primary actions and selection
- Emerald (#10b981) used exclusively for success/confirmation states
- Compact 13px body text with Space Grotesk headlines for structured readability
- All interactive components use 0.2s transitions with ease-out curves
- Focus rings use 2px solid primary with 2px offset for keyboard accessibility

## 2. Colors: The Obsidian Palette

A restrained palette where near-black zinc surfaces provide depth through tonal layering, electric indigo signals interactivity, and semantic colors speak clearly without competing.

### Primary
- **Electric Indigo** (#6366f1): Primary actions, active selections, focus rings, surface tint. Used on buttons, active sidebar items, badges for "current" state, links. The only saturated color that appears in resting UI.
- **Indigo Container** (#818cf8): Lighter indigo for secondary emphasis, hover accents on indigo surfaces, tag/badge backgrounds at reduced opacity (8-15%).
- **Deep Indigo** (#4f46e5): Pressed/active state for primary buttons. Inverse primary for light-on-dark inversions.

### Secondary
- **Emerald Accent** (#10b981): Success confirmation, "created" badges, positive state indicators. Never decorative; always signals a completed or positive outcome.
- **Emerald Container** (#064e3b): Background tint for success banners and tags at low opacity.

### Neutral
- **Obsidian** (#09090b): Root background. The deepest surface.
- **Obsidian Raised** (#121214): Card and panel backgrounds. One step above root.
- **Obsidian Surface** (#18181b): Container-level surfaces, table headers at reduced opacity.
- **Obsidian Muted** (#27272a): Borders at 15-30% opacity, secondary button backgrounds, hover states on surfaces.
- **Obsidian Bright** (#3f3f46): Highest surface step, skeleton shimmer endpoint, active hover backgrounds.
- **Zinc Text** (#f4f4f5): Primary text. High contrast against all obsidian surfaces (>15:1).
- **Zinc Secondary** (#a1a1aa): Secondary text, descriptions, timestamps. Contrast ratio 6.5:1 against obsidian.
- **Zinc Subtle** (#71717a): Tertiary text, placeholders, disabled labels. Contrast ratio 4.6:1.

### Semantic
- **Coral Error** (#f87171): Destructive actions, validation errors, delete confirmations.
- **Amber Warning** (#f59e0b): Caution states, unsaved changes indicators.
- **Mint Success** (#34d399): Inline success messages, active/healthy status indicators.

### Named Rules
**The Indigo Discipline Rule.** Electric Indigo (#6366f1) appears on less than 10% of any given screen. Its rarity makes interactivity self-evident: if something is indigo, you can act on it.

**The Tonal Depth Rule.** Surface hierarchy is expressed through tonal steps (obsidian → raised → surface → muted → bright), never through shadows at rest. The six-step zinc ramp IS the elevation system.

## 3. Typography

**Headline Font:** Space Grotesk (with sans-serif fallback)
**Body Font:** Inter (with system-ui, -apple-system, BlinkMacSystemFont, Segoe UI, sans-serif fallback)
**Mono Font:** JetBrains Mono (with ui-monospace, SFMono-Regular, monospace fallback)

**Character:** Space Grotesk gives headlines geometric precision without coldness; Inter provides invisible, comfortable reading for the dense data and form labels that fill admin surfaces. JetBrains Mono signals "this is a technical identifier" in slugs, keys, and system IDs.

### Hierarchy
- **Headline** (700, 17px, 1.3 line-height, -0.01em tracking): Page titles, section headers, card titles. Space Grotesk. Always the largest element in its container.
- **Body** (400, 13px, 1.5 line-height): Primary content text, form labels, descriptions. Inter. Maximum line length 65ch for prose blocks.
- **Body Semibold** (600, 13px, 1.5 line-height): Inline emphasis, actor names in audit rows, active values in tables.
- **Label** (500, 11px, 1.0 line-height, 0.05em tracking, uppercase): Table column headers, section labels, metadata tags. Inter. Always uppercase with wide tracking.
- **Caption** (400, 10.5-11px, 1.4 line-height): Timestamps, system IDs, secondary metadata. Inter or JetBrains Mono depending on content type.
- **Mono** (400, 11px, 1.5 line-height): Keys, slugs, configuration identifiers, code values. JetBrains Mono. Slightly smaller than body to not dominate.

### Named Rules
**The Three-Family Rule.** No more than three font families on any screen: Space Grotesk for headlines, Inter for everything else, JetBrains Mono for technical identifiers. A fourth family is always wrong.

## 4. Elevation

This system is flat by default. Depth is expressed entirely through the six-step tonal surface hierarchy (obsidian #09090b → bright #3f3f46), not through box shadows. Surfaces sit flush with each other; their relative lightness communicates their position in the stack.

Shadows appear only as responses to state: primary buttons carry a subtle `shadow-md` at rest (the only exception to flat-at-rest), and command palette/modals use `0 10px 15px -3px rgba(0,0,0,0.5)` as a structural separation from the content below. No decorative shadows on cards, panels, or table rows.

### Shadow Vocabulary
- **Button ambient** (`shadow-md` via Tailwind): Primary and destructive buttons at rest. Provides minimal lift to signal clickability.
- **Modal structural** (`0 10px 15px -3px rgba(0,0,0,0.5), 0 4px 6px -2px rgba(0,0,0,0.5)`): Command palette, confirm dialogs, flatpickr calendar. Heavy enough to separate overlay from page content.
- **Focus glow** (`0 0 0 3px color-mix(in srgb, var(--primary) 20%, transparent)`): Input focus state. Not a shadow for elevation; a glow to signal keyboard position.

### Named Rules
**The Flat-By-Default Rule.** Surfaces are flat at rest. Shadows appear only as a response to state (hover, elevation, focus) or to structurally separate overlays from content. If you're adding a shadow to a card, it's wrong.

## 5. Components

### Buttons
- **Shape:** Rounded-lg (12px radius), 44px height at default size.
- **Primary:** Electric Indigo background (#6366f1), white text, shadow-md. Hover: brightness-105 filter. Active: scale(0.98) press feedback. Focus: 2px primary ring at 40% opacity.
- **Secondary:** Obsidian Muted background (#27272a), zinc-secondary text. Hover: brightens to surface-bright, text shifts to zinc-text.
- **Ghost:** Transparent background, zinc-subtle text. Hover: surface-container-low background.
- **Destructive:** Coral Error background (#f87171), white text.
- **All variants:** 13px font-bold, -0.01em tracking, 0.2s transition-all, disabled at 40% opacity with pointer-events-none.

### Inputs / Fields
- **Shape:** Rounded-xl (16px radius), 44px height, 13px text.
- **Background:** Surface-container-lowest (#030303), 1px outline-variant border at 20% opacity.
- **Focus:** Border shifts to primary, 2px ring at primary/20%. No background change.
- **With icon:** 18px Material Symbol icon positioned left at outline/60% opacity, input padded left to 40px.
- **Disabled:** 40% opacity, not-allowed cursor.

### Cards / Containers
- **Corner Style:** Rounded-2xl (16px radius).
- **Background:** Obsidian Raised (#121214) via surface-container-low.
- **Shadow Strategy:** None at rest (Flat-By-Default Rule). Hover state on interactive cards adds background shift to surface-container.
- **Border:** 1px outline-variant at 15% opacity. Subtle enough to define edges without creating visual weight.
- **Internal Padding:** 20px (p-5).

### Tables
- **Header:** Surface-container-lowest at 50% opacity background. 11px uppercase labels with 0.05em tracking.
- **Rows:** Divided by 1px outline-variant at 10% opacity. Hover: surface-container-high at 20-40% opacity with 0.15s transition.
- **Interactive rows (audit logs):** Cursor pointer, group hover with text color shifts on timestamps.

### Chips / Badges
- **Style:** Rounded-md (8px), 10-11px font-medium, semantic background at 8-15% opacity with matching text color.
- **Variants:** Primary (indigo/8%), Success (success/10%), Error (error/8%), Neutral (surface-container-highest + outline text).
- **No border on semantic chips.** The tinted background carries the meaning.

### Navigation (Sidebar)
- **Width:** 256px expanded, 64px collapsed. Transition: 0.25s ease width, 0.3s ease transform.
- **Active item:** Primary text color, primary/8% background tint. 2px left accent via background, not border.
- **Hover:** Surface-container-high/40% background.
- **Mobile:** Full-width overlay with backdrop.

### Skeleton Loading
- **Shimmer:** Linear gradient from surface-container-high through surface-container-highest back to surface-container-high, 800px background-size, 1.5s ease-in-out infinite animation.
- **Border radius:** 6px default (rounded-md). Match the shape of the content being replaced.
- **Layout preservation:** Skeleton dimensions must match final content dimensions to prevent CLS.

## 6. Do's and Don'ts

### Do:
- **Do** express depth through the six-step tonal surface hierarchy, not through shadows.
- **Do** use Electric Indigo (#6366f1) exclusively for interactive elements: buttons, active states, links, focus rings.
- **Do** keep body text at 13px Inter for density-appropriate admin readability.
- **Do** use uppercase 11px labels with 0.05em tracking for all table headers and section labels.
- **Do** provide skeleton loaders that match the final content layout for every async data load.
- **Do** use 0.2s ease-out transitions for all interactive state changes.
- **Do** translate technical settings into plain language with helpful hints (PRODUCT.md: "Explain, Don't Assume").
- **Do** build confirmation steps and clear input validations for destructive actions (PRODUCT.md: "Preventative Guardrails").
- **Do** maintain WCAG 2.1 AA contrast ratios: zinc-text (#f4f4f5) on obsidian surfaces achieves >15:1.

### Don't:
- **Don't** use border-left or border-right greater than 1px as a colored accent stripe on cards, list items, or alerts.
- **Don't** apply gradient text (background-clip: text with gradients) anywhere.
- **Don't** add shadows to cards or panels at rest. The Flat-By-Default Rule is absolute.
- **Don't** use confusing developer-heavy dashboards with cryptic acronyms or code dumps (PRODUCT.md anti-reference).
- **Don't** use loud gradients, unnecessary animations, or low-contrast text (PRODUCT.md anti-reference).
- **Don't** ship interfaces that lack inline validation, explanations, or safety nets for configuration changes (PRODUCT.md anti-reference).
- **Don't** use a fourth font family beyond Space Grotesk, Inter, and JetBrains Mono.
- **Don't** use glassmorphism or backdrop-blur as decoration. The command palette backdrop is the only permitted blur surface.
- **Don't** use spinners in the middle of content areas. Use skeleton loaders that mirror the final layout.
- **Don't** use Indigo on non-interactive elements. If it's indigo and you can't click it, it's wrong.
- **Don't** exceed 10% indigo coverage on any single screen.
