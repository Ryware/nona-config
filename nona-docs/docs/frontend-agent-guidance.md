# Frontend Agent Guidance

This guide helps agents apply the Nuxt frontend rules in `AGENTS.md` without
turning every task into a broad refactor. `AGENTS.md` remains the authoritative
surface for hard invariants. Use this document for examples, risk-based
judgment, and choosing the smallest useful validation.

## Rule Strength

Read frontend guidance by strength:

- **Must**: hard invariants. Examples: keep the Bun workspace, do not add React
  or Radix React dependencies to Nuxt, do not add Nuxt Prettier, and preserve
  public behavior unless the task explicitly changes it.
- **Default**: normal posture for new or meaningfully refactored code. Examples:
  one component per directory, colocated contracts for non-trivial components,
  and `useBMC` for Vue component root classes.
- **Prefer**: use this unless the local code or task gives a clear reason not
  to. Examples: named functions for composables, `undefined` for optional app
  values, existing DustHowl UI building blocks before new blocks.
- **Opportunistically**: clean nearby issues only when low-risk and already in
  the touched area. Do not widen a narrow fix into a family migration.
- **Ask or plan first**: broad component moves, visual redesigns, public API
  changes, dependency changes, or validation-script changes.

When guidance conflicts, keep the hard invariants from `AGENTS.md`, then choose
the smallest local improvement that preserves behavior.

## Frontend Change Classification

Classify the change before editing. This keeps validation and refactor scope
proportional.

| Change shape | Normal scope | Typical validation |
| --- | --- | --- |
| Docs-only | Update docs or guidance only | `git diff --check` |
| Narrow bug fix | Fix the failing behavior; avoid file moves | Focused lint/test for touched files |
| Touched component cleanup | Improve nearby code already being edited | Focused ESLint/stylelint and targeted tests if present |
| Component-family refactor | Move or split related components | Focused checks plus import/registration review |
| Shared UI change | Change reusable UI layer behavior or styling | Focused checks plus relevant fixture or visual scenario |
| Broad visual change | Layout, responsive, theme, or interaction changes | Desktop/mobile visual evidence and review helpers |
| Module/runtime change | API, realtime, auth, or app plumbing | Focused tests/typecheck and smoke checks |

If the task begins as a narrow bug fix, keep structure stable unless the current
structure blocks a correct fix. If structure is the problem, call that out and
move only the smallest related set.

## Component Structure

Use one component per directory when creating or meaningfully refactoring Vue
components. The main file should mirror the directory name.

```text
user-card/
  index.ts
  user-card.vue
```

Keep a trivial one-off component flat when it is unlikely to grow and has no
meaningful child structure.

```text
empty-state/
  empty-state.vue
```

For tightly related child components, nest under the parent and remove repeated
parent prefixes from child directory and file names.

```text
voice-channel-panel/
  controls/
    controls.vue
  user-list/
    user-list.vue
```

When a semantic prefix family has more than two related components, or could
reasonably grow beyond two, create a parent directory named for the shared
prefix.

```text
message/
  content/
    content.vue
  group/
    group.vue
  open-graph/
    open-graph.vue
```

Avoid growing sibling families like this:

```text
message-content/message-content.vue
message-group/message-group.vue
message-open-graph/message-open-graph.vue
```

When moving existing components, preserve current behavior and registration
expectations. Check call sites, auto-import names, test imports, and story or
fixture references. If preserving compatibility needs wrappers or a staged
caller migration, plan that explicitly instead of silently breaking imports.

## Props, Emits, And Contracts

In the DustHowl UI layer, inline props and emits are not acceptable. Put
component props, emits, and exported contracts in a nearby `index.ts`, even for
small components, so shared UI contracts stay discoverable and reusable.

Outside the UI layer, inline props and emits are acceptable for trivial leaf
components when all of these are true:

- no emitted events, or only a private implementation detail;
- no exported contract used by parents, tests, or fixtures;
- one or two primitive props at most;
- no variants, modifiers, or mode-specific behavior.

Use a nearby `index.ts` outside the UI layer when a component is reusable, has
emitted events, has variants or modifiers, exports types consumed elsewhere, or
the refactor touches template/script contract logic.

```text
message/content/
  index.ts
  content.vue
```

Name contracts after the component:

```ts
export interface MessageContentProps {
  text: string;
  edited?: boolean;
}

export interface MessageContentEmits {
  retry: [];
}
```

Prefer object payloads for helpers once a config grows past two or three fields.
Keep the object as `config` or `payload` in the function signature and read
fields inside the function body.

## BEM And useBMC

For every new or meaningfully refactored Vue component root, default to
`useBMC` from `@rhapsodic/bem-classnames-vue`.

Meaningfully refactored means the component's template or script class logic is
touched beyond a narrow bug fix. Examples:

- adding a root state class;
- changing modifiers or variants;
- replacing manual class objects;
- splitting the component and rewriting its root template;
- moving a component into a new component-family structure.

A static root class is fine for a trivial leaf with no props, modifiers,
variants, dynamic class state, or nearby class logic.

Do not call `useBMC` with an empty settings object only to produce the base
block class. If a component has no modifiers, variants, or reactive class state,
use a literal static root class instead.

```vue
<template>
  <span class="typing-dot" />
</template>
```

Use `useBMC` when root state is reactive.

```vue
<script setup lang="ts">
import { flag, useBMC, variant } from '@rhapsodic/bem-classnames-vue';

const props = defineProps<MessageContentProps>();

const classNames = useBMC(props, 'message-content', {
  edited: flag('state', 'edited'),
  tone: variant('tone', {
    primary: 'primary',
    muted: 'muted'
  })
});
</script>

<template>
  <article :class="classNames">
    <slot />
  </article>
</template>
```

When converting existing manual classes, preserve the established block name and
modifiers unless the task explicitly includes a naming migration.

## Shared UI Versus App Widgets

Keep app-specific workflow in `frontend/nuxt/app/components/widgets`.
Examples include channel workflows, message composer flows, or settings flows
that depend on app stores and routes.

Put reusable presentation and controls in
`frontend/nuxt/layers/dusthowl-ui/app/components/ui`. A component is a promotion
candidate when it is useful in two or more unrelated widgets, has no app-store
dependency, and can be described as a generic UI building block.

Decision tree:

1. Is it tied to one product workflow or store? Keep it in app widgets.
2. Is it generic presentation or an input/control? Prefer the UI layer.
3. Is it duplicated in unrelated widgets? Consider promotion.
4. Would promotion change API, styling, or behavior for existing callers? Plan
   the migration and add focused validation.

Do not introduce a shared abstraction only because two snippets look similar.
Promote when the boundary is stable and reuse reduces real complexity.

## Styling And Visual Judgment

Preserve the established DustHowl visual language unless the user asks for a
change or the current UI has an obvious issue. Obvious issues include overflow,
overlap, inaccessible focus states, unreadable contrast, broken responsive
layout, or inconsistent interaction states.

Prefer existing theme tokens and shared patterns over one-off values. Keep
component-specific custom properties inside the block rule and name them with
the block prefix.

```scss
.message-content {
  --message-content__accent: var(--dh-color-accent);

  &__body {
    color: var(--dh-color-text);
  }
}
```

Use `v-bind(...)` in component styles when the value is component-scoped style
state. Prefer class modifiers when the state is semantic and reused by multiple
rules.

If hover and keyboard focus should look the same, define `:hover` and
`:focus-visible` together. Define `:active` separately.

## Visual Validation Matrix

Use the smallest validation that can prove the change. Add visual evidence when
the user-visible surface changes.

| Change type | Minimum validation |
| --- | --- |
| Docs or comments only | `git diff --check -- <files>` |
| Vue script-only behavior | Focused ESLint and targeted unit/composable test if available |
| Vue template or SCSS change | Focused ESLint, stylelint, and screenshot when user-visible |
| Shared UI component | Focused checks plus relevant fixture route or visual scenario |
| Component-family move | Focused checks plus import/auto-registration review |
| Broad visual refactor | Desktop and mobile screenshots plus visual helper review |
| Runtime/module plumbing | Focused tests/typecheck and smoke check |

For Nuxt iteration, prefer focused checks such as:

```bash
bun --cwd frontend/nuxt eslint app/path/to/file.vue --cache
bun --cwd frontend/nuxt stylelint app/path/to/file.vue --cache --allow-empty-input
git diff --check -- app/path/to/file.vue
```

Do not run broad Nuxt generate/typecheck gates unless the task or handoff
requires them.

## Existing Visual Helpers

Prefer the existing `packages/e2e` visual helpers before writing one-off
Playwright code.

```bash
bun run capture:visual -- --scenario media-lab
bun run verify:visual -- --out-root /tmp/dusthowl-visual/<surface>
bun run review:visual -- --out-root /tmp/dusthowl-visual/<surface>
```

Use dense seeded fixtures such as `message-lab`, `media-lab`, populated settings
tabs, and voice fixtures when they cover the surface. Save artifacts under
`/tmp/dusthowl-visual/<surface>`.

Avoid `networkidle` in visual scripts because realtime sockets and dev servers
can remain busy. Prefer `waitUntil: 'commit'` plus explicit readiness selectors
and text checks.

This guide does not authorize changing frontend scripts or e2e helper
implementations. If a helper seems awkward or duplicated, note a follow-up
unless the current task explicitly includes script or e2e helper improvements.

## When To Ask Or Plan First

Ask, run a planning workflow, or create a narrow handoff before:

- changing dependencies or package manager behavior;
- changing Nuxt formatting tools or adding Prettier;
- moving a large component family across public import or auto-registration
  boundaries;
- changing shared UI APIs used by multiple widgets;
- redesigning visual language rather than fixing a concrete issue;
- changing frontend scripts or e2e helper implementations;
- broad validation that would require long-running servers or expensive gates.

Proceed directly when the change is local, reversible, behavior-preserving, and
covered by focused validation.
