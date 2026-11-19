# Solid 2 migration status

This draft upgrades the dependency set and build configuration. It is not ready to merge.

## Remaining work

- Replace the 1.x router components (`Router`, `Route`, `A`, and `Navigate`) with the Solid Router 2 `createRouter` API in the app and integration tests.
- Remove `MetaProvider`; Solid Meta 2 registers head tags without a provider.
- Migrate `onMount`, one-argument `createEffect`, `on`, `splitProps`, `Index`, context providers, store path setters, and JSX types to Solid 2 APIs.
- Update native JSX attributes to Solid 2 names and values, and migrate TanStack Solid Query calls to v6.
- Check Kobalte 2 alpha against the final Solid 2 release. Its published peer dependency still names Solid 2 RC. Remove the local npm peer override when this is resolved upstream.
- Run typecheck, lint, unit and integration tests, production build, and targeted browser checks.

The pre-migration `npm run build` passed on the original branch. The upgraded dependency tree installs with `npm ci`; source migration is still in progress.
