// @ts-check
import boundaries from "eslint-plugin-boundaries";
import solid from "eslint-plugin-solid";
import tseslint from "typescript-eslint";

export default tseslint.config(
  // ── TypeScript base ────────────────────────────────────────────────────────
  ...tseslint.configs.recommended,

  // ── Global ignores ─────────────────────────────────────────────────────────
  {
    ignores: [
      "dist/**",
      "node_modules/**",
      "src/shared/api/generated.ts",
      "*.config.js",
      "*.config.ts",
      "postcss.config.js",
    ],
  },

  // ── SolidJS rules ──────────────────────────────────────────────────────────
  {
    files: ["src/**/*.{ts,tsx}"],
    plugins: { solid },
    rules: {
      ...solid.configs.typescript.rules,
    },
  },

  // ── Module boundary rules (eslint-plugin-boundaries v6) ───────────────────
  {
    files: ["src/**/*.{ts,tsx}"],
    plugins: { boundaries },
    settings: {
      "boundaries/elements": [
        { type: "app",      pattern: "src/app/**" },
        { type: "shared",   pattern: "src/shared/**" },
        { type: "entities", pattern: "src/entities/**" },
        { type: "features", pattern: "src/features/**" },
        { type: "widgets",  pattern: "src/widgets/**" },
        { type: "pages",    pattern: "src/pages/**" },
      ],
    },
    rules: {
      // boundaries v6 renamed element-types → dependencies
      "boundaries/dependencies": [
        "error",
        {
          default: "disallow",
          rules: [
            { from: "shared",   allow: ["shared"] },
            { from: "entities", allow: ["shared", "entities"] },
            { from: "features", allow: ["shared", "entities", "features"] },
            { from: "widgets",  allow: ["shared", "entities", "features", "widgets"] },
            { from: "pages",    allow: ["shared", "entities", "features", "widgets"] },
            { from: "app",      allow: ["shared", "entities", "features", "widgets", "pages", "app"] },
          ],
        },
      ],
    },
  },

  // ── TypeScript strict rules ────────────────────────────────────────────────
  {
    files: ["src/**/*.{ts,tsx}"],
    rules: {
      "@typescript-eslint/no-explicit-any": "error",
      "@typescript-eslint/no-unused-vars": ["error", { argsIgnorePattern: "^_", varsIgnorePattern: "^_" }],
      "@typescript-eslint/consistent-type-imports": ["error", { prefer: "type-imports" }],
    },
  },

  // ── Relax rules for test files ─────────────────────────────────────────────
  {
    files: ["src/__tests__/**/*.{ts,tsx}"],
    rules: {
      "@typescript-eslint/no-explicit-any": "off",
      "boundaries/element-types": "off",
    },
  },
);
