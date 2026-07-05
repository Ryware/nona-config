import { describe, it, expect } from 'vitest';
import { readdirSync, existsSync } from 'node:fs';
import { join } from 'node:path';

const ROOT = process.cwd();
const PAGES_DIR = join(ROOT, 'src', 'pages');
const INTEGRATION_DIR = join(ROOT, 'src', '__tests__', 'integration');

function findPageComponents(dir: string): string[] {
  const results: string[] = [];
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const fullPath = join(dir, entry.name);
    if (entry.isDirectory()) {
      results.push(...findPageComponents(fullPath));
    } else if (entry.name.endsWith('Page.tsx')) {
      results.push(entry.name.replace('.tsx', ''));
    }
  }
  return results;
}

describe('Integration test coverage', () => {
  it('every page component has a corresponding integration test file', () => {
    const pages = findPageComponents(PAGES_DIR);

    const missing = pages.filter(
      (page) => !existsSync(join(INTEGRATION_DIR, `${page}.test.tsx`)),
    );

    expect(
      missing,
      `The following pages have no integration test:\n  ${missing.map((p) => `src/pages/**/${p}.tsx`).join('\n  ')}\n\nAdd a test file at src/__tests__/integration/<PageName>.test.tsx for each.`,
    ).toHaveLength(0);
  });
});
