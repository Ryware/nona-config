# Nona Docs

Starlight documentation site for Nona.

## Commands

| Command | Action |
|---|---|
| `npm run dev` | Start the local docs server. |
| `npm run generate:cli` | Regenerate `src/content/docs/cli/reference.md` from the Nona CLI help output. |
| `npm run build` | Regenerate CLI docs and build the static site. |
| `npm run archive` | Create `artifacts/docs-html.zip` from `dist/`. |
| `npm run ci` | Build and archive the docs site. |
| `npm run preview` | Preview `dist/` locally. |

`generate:cli` expects the monorepo root one directory above `docs`. Override with `NONA_BACKEND_DIR=/path/to/nona-config`.

## CI

GitHub Actions builds the static HTML site and uploads the zip archive as a workflow artifact. It does not create releases or deploy the site.
