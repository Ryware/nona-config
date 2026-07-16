import { expect, type Page, test } from '@playwright/test';

const project = {
  id: 'proj-1',
  urlSlug: 'my-app',
  name: 'my-app',
  description: 'Main application config',
  environments: ['production', 'staging'],
  createdAt: '2024-01-01T00:00:00Z',
  updatedAt: '2024-01-01T00:00:00Z',
};

const users = [
  {
    id: 'user-1',
    email: 'admin@example.com',
    name: 'Admin User',
    isAdmin: true,
    role: 'admin',
    scope: 'all',
    createdAt: '2024-01-01T00:00:00Z',
    updatedAt: '2024-01-01T00:00:00Z',
  },
];

const environments = [
  {
    project: 'my-app',
    name: 'production',
    activeReleaseVersion: '1.0.0',
    createdAt: '2024-01-01T00:00:00Z',
    updatedAt: '2024-01-01T00:00:00Z',
  },
  {
    project: 'my-app',
    name: 'staging',
    activeReleaseVersion: null,
    createdAt: '2024-01-01T00:00:00Z',
    updatedAt: '2024-01-01T00:00:00Z',
  },
];

const releases = [
  {
    project: 'my-app',
    environment: 'production',
    version: '1.0.0',
    entryCount: 3,
    isActive: true,
    createdAt: '2024-01-01T00:00:00Z',
    actor: 'admin@example.com',
  },
  {
    project: 'my-app',
    environment: 'production',
    version: '1.1.0',
    entryCount: 3,
    isActive: false,
    createdAt: '2024-01-02T00:00:00Z',
    actor: 'admin@example.com',
  },
];

const configEntries = [
  {
    project: 'my-app',
    environment: 'production',
    key: 'API_URL',
    value: 'https://api.example.com',
    contentType: 'text',
    scope: 'server',
    activeVersion: 1,
    createdAt: '2024-01-01T00:00:00Z',
    updatedAt: '2024-01-01T00:00:00Z',
  },
  {
    project: 'my-app',
    environment: 'production',
    key: 'MAX_RETRIES',
    value: '3',
    contentType: 'number',
    scope: 'all',
    activeVersion: 1,
    createdAt: '2024-01-01T00:00:00Z',
    updatedAt: '2024-01-01T00:00:00Z',
  },
  {
    project: 'my-app',
    environment: 'production',
    key: 'FEATURE_FLAGS',
    value: '{"dark_mode": true}',
    contentType: 'json',
    scope: 'client',
    activeVersion: 1,
    createdAt: '2024-01-01T00:00:00Z',
    updatedAt: '2024-01-01T00:00:00Z',
  },
];

const apiKeys = [
  {
    id: 'key-1',
    name: 'Web Client',
    key: 'ak_test_1234567890abcdef',
    project: 'my-app',
    environment: 'production',
    scope: 'client',
    createdAt: '2024-01-01T00:00:00Z',
    updatedAt: '2024-01-01T00:00:00Z',
  },
];

async function mockApi(page: Page) {
  await page.route('**/admin/**', async route => {
    const url = new URL(route.request().url());
    const path = url.pathname;

    if (route.request().method() === 'GET' && path === '/admin/projects') {
      await route.fulfill({ json: [project] });
      return;
    }

    if (route.request().method() === 'GET' && path === '/admin/users') {
      await route.fulfill({ json: users });
      return;
    }

    if (route.request().method() === 'GET' && path === '/admin/projects/my-app/environments') {
      await route.fulfill({ json: environments });
      return;
    }

    if (route.request().method() === 'GET' && path === '/admin/projects/my-app/api-keys') {
      await route.fulfill({ json: apiKeys });
      return;
    }

    if (
      route.request().method() === 'GET' &&
      path === '/admin/projects/my-app/environments/production/releases'
    ) {
      await route.fulfill({ json: releases });
      return;
    }

    if (
      route.request().method() === 'GET' &&
      path === '/admin/projects/my-app/environments/production/config-entries'
    ) {
      await route.fulfill({ json: configEntries });
      return;
    }

    if (
      route.request().method() === 'POST' &&
      path === '/admin/projects/my-app/environments/production/releases/1.1.0/draft'
    ) {
      await route.fulfill({ json: configEntries });
      return;
    }

    await route.fulfill({ status: 404, json: { error: `Unhandled visual test route: ${path}` } });
  });
}

async function signIn(page: Page) {
  await page.addInitScript(() => {
    localStorage.setItem('auth_token', 'visual-test-token');
    localStorage.setItem(
      'auth_session',
      JSON.stringify({ email: 'admin@example.com', role: 'admin', isAdmin: true }),
    );
    localStorage.setItem('sidebar_collapsed', 'true');
  });
}

test.beforeEach(async ({ page }) => {
  await signIn(page);
  await mockApi(page);
});

test('project release page matches approved screenshot', async ({ page }, testInfo) => {
  const browserErrors = collectBrowserErrors(page);

  await page.goto('/projects/my-app');
  await expect(page.getByTestId('project-detail-heading')).toHaveText('my-app');
  await expect(page.getByRole('heading', { name: 'Releases' })).toBeVisible();
  await expect(page.getByText('1.0.0').first()).toBeVisible();
  await expect(page.getByText('1.1.0')).toBeVisible();
  await expectNoHorizontalOverflow(page);

  if (testInfo.project.name.includes('desktop')) {
    const releasePanel = page.locator('section').filter({
      has: page.getByRole('heading', { name: 'Releases' }),
    });

    await expect(page).toHaveScreenshot('project-release-page-desktop.png', { fullPage: true });
    await expect(releasePanel).toHaveScreenshot('release-panel-desktop.png');
  } else {
    await expect(page).toHaveScreenshot('project-release-page-mobile.png', { fullPage: true });
  }

  expect(browserErrors).toEqual([]);
});

test('release draft confirmation matches approved screenshot', async ({ page }) => {
  const browserErrors = collectBrowserErrors(page);

  await page.goto('/projects/my-app');
  await expect(page.getByRole('heading', { name: 'Releases' })).toBeVisible();
  await page.getByTestId('release-draft-1.1.0').click();
  await expect(page.getByRole('dialog')).toBeVisible();
  await expect(page.getByRole('dialog')).toContainText('Load Release into Workspace');
  await expect(page.getByRole('dialog')).toHaveScreenshot('release-draft-dialog.png');
  await expectNoHorizontalOverflow(page);
  expect(browserErrors).toEqual([]);
});

test('release delete confirmation matches approved screenshot', async ({ page }) => {
  const browserErrors = collectBrowserErrors(page);

  await page.goto('/projects/my-app');
  await expect(page.getByRole('heading', { name: 'Releases' })).toBeVisible();
  await page.getByTestId('release-delete-1.1.0').click();
  await expect(page.getByRole('dialog')).toBeVisible();
  await expect(page.getByRole('dialog')).toContainText('Delete Release');
  await expect(page.getByRole('dialog')).toHaveScreenshot('release-delete-dialog.png');
  await expectNoHorizontalOverflow(page);
  expect(browserErrors).toEqual([]);
});

function collectBrowserErrors(page: Page) {
  const errors: string[] = [];
  page.on('console', message => {
    if (message.type() === 'error') {
      errors.push(message.text());
    }
  });
  page.on('pageerror', error => errors.push(error.message));
  return errors;
}

async function expectNoHorizontalOverflow(page: Page) {
  const hasOverflow = await page.evaluate(
    () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
  );
  expect(hasOverflow).toBe(false);
}
