import { expect, type Locator, type Page, test } from '@playwright/test';

const project = {
  id: 'proj-1',
  urlSlug: 'my-app',
  name: 'my-app',
  accessLevel: 'admin',
  description: 'Storefront configuration',
  environments: ['production', 'staging'],
  createdAt: '2026-08-01T08:00:00Z',
  updatedAt: '2026-08-20T08:00:00Z',
};

const environments = [
  {
    project: 'my-app',
    name: 'production',
    activeReleaseVersion: '2.0.0',
    createdAt: '2026-08-01T08:00:00Z',
    updatedAt: '2026-08-20T08:00:00Z',
  },
  {
    project: 'my-app',
    name: 'staging',
    activeReleaseVersion: null,
    createdAt: '2026-08-01T08:00:00Z',
    updatedAt: '2026-08-20T08:00:00Z',
  },
];

const releases = [
  {
    project: 'my-app',
    environment: 'production',
    version: '2.0.0',
    entryCount: 11,
    isActive: true,
    createdAt: '2026-08-19T08:00:00Z',
    actor: 'alice@example.com',
  },
];

const baseEntry = {
  project: 'my-app',
  environment: 'production',
  activeVersion: 2,
  createdAt: '2026-08-10T08:00:00Z',
  updatedAt: '2026-08-20T08:00:00Z',
};

const configEntries = [
  {
    ...baseEntry,
    key: 'Checkout:FreeShippingThreshold',
    value: '25',
    contentType: 'number',
    scope: 'client',
    description: 'Minimum cart value for free shipping.',
    unit: 'USD',
  },
  {
    ...baseEntry,
    key: 'Checkout:MaxCartItems',
    value: '50',
    contentType: 'number',
    scope: 'all',
    description: 'Maximum number of items allowed in one cart.',
    unit: 'items',
  },
  {
    ...baseEntry,
    key: 'Checkout:Taxes:DefaultRegion',
    value: 'US',
    contentType: 'text',
    scope: 'server',
    description: 'Fallback region used for tax calculation.',
    unit: null,
  },
  {
    ...baseEntry,
    key: 'Features:Checkout',
    value: 'true',
    contentType: 'boolean',
    scope: 'client',
    description: 'Controls the new checkout experience.',
    unit: null,
  },
  {
    ...baseEntry,
    key: 'Features:LiveChat',
    value: 'false',
    contentType: 'boolean',
    scope: 'client',
    description: 'Shows live chat to eligible customers.',
    unit: null,
  },
  {
    ...baseEntry,
    key: 'Features:Search:Fuzzy',
    value: 'true',
    contentType: 'boolean',
    scope: 'client',
    description: 'Enables tolerant matching in storefront search.',
    unit: null,
  },
  {
    ...baseEntry,
    key: 'Features:Search:Rules',
    value: '{"minScore":0.72,"boostExact":true}',
    contentType: 'json',
    scope: 'server',
    description: 'Ranking rules used by search.',
    unit: null,
  },
  {
    ...baseEntry,
    key: 'Killswitch:Payments',
    value: 'false',
    contentType: 'boolean',
    scope: 'all',
    description: 'Emergency stop for payment processing.',
    unit: null,
  },
  {
    ...baseEntry,
    key: 'Payments:ProviderTimeoutMs',
    value: '2500',
    contentType: 'number',
    scope: 'server',
    description: 'Maximum payment provider response time.',
    unit: 'ms',
  },
  {
    ...baseEntry,
    key: 'Region',
    value: 'global',
    contentType: 'text',
    scope: 'all',
    description: 'Default service region.',
    unit: null,
  },
  {
    ...baseEntry,
    key: 'Region:DisplayName',
    value: 'Global',
    contentType: 'text',
    scope: 'client',
    description: 'Customer-facing region label.',
    unit: null,
  },
];

const history = [
  {
    project: 'my-app',
    environment: 'production',
    key: 'Checkout:FreeShippingThreshold',
    version: 2,
    value: '25',
    contentType: 'number',
    scope: 'client',
    description: 'Minimum cart value for free shipping.',
    unit: 'USD',
    createdAt: '2026-08-20T08:00:00Z',
    actor: 'alice@example.com',
  },
  {
    project: 'my-app',
    environment: 'production',
    key: 'Checkout:FreeShippingThreshold',
    version: 1,
    value: '35',
    contentType: 'number',
    scope: 'client',
    description: 'Original free shipping threshold.',
    unit: 'USD',
    createdAt: '2026-08-18T14:30:00Z',
    actor: 'bob@example.com',
  },
];

test.use({ colorScheme: 'light', locale: 'en-US', timezoneId: 'UTC' });

test.beforeEach(async ({ page }) => {
  await signIn(page);
  await mockApi(page);
});

test('compact live tree exposes the shared inline editors', async ({ page }, testInfo) => {
  test.skip(!testInfo.project.name.includes('desktop'), 'Desktop screenshot coverage');
  const browserErrors = collectBrowserErrors(page);

  await page.goto('/projects/my-app');
  const section = page.getByTestId('project-parameters-section');
  await expect(section).toBeVisible();
  await expectCompactParameters(page);
  await expect(page.getByTestId('parameter-group-Checkout')).toHaveAttribute('aria-expanded', 'true');
  await expect(page.getByTestId('parameter-group-Checkout:Taxes')).toHaveAttribute('aria-expanded', 'true');
  await expect(page.getByTestId('parameter-group-Features:Search')).toHaveAttribute('aria-expanded', 'true');
  await expect(page.getByTestId('parameter-group-Region')).toHaveAttribute('aria-expanded', 'true');
  await expect(page.getByTestId('parameter-display-Region')).toHaveText('Region');
  await expect(page.getByTestId('parameter-display-Region:DisplayName')).toHaveText('DisplayName');
  await expect(page.getByTestId('parameter-value-input-Features:Checkout')).toHaveRole('switch');
  await expect(page.getByTestId('parameter-value-input-Payments:ProviderTimeoutMs')).toHaveValue('2500');
  await expect(page.getByLabel('Unit ms')).toBeVisible();
  await expect(page.getByTestId('parameter-value-input-Region')).toHaveValue('global');
  await expect(page.getByTestId('parameter-value-input-Features:Search:Rules')).toHaveValue(
    '{"minScore":0.72,"boostExact":true}',
  );
  await expect(page.getByTestId('parameter-update-Features:Checkout')).toBeVisible();
  await expect(page.getByTestId('parameter-update-Payments:ProviderTimeoutMs')).toBeVisible();
  await expect(page.getByTestId('parameter-update-Region')).toBeVisible();
  await expect(page.getByTestId('parameter-update-Features:Search:Rules')).toBeVisible();

  await expect(section).toHaveScreenshot('parameter-live-compact.png');
  await expectNoHorizontalOverflow(page);
  expect(browserErrors).toEqual([]);
});

test('compact Settings panel keeps the list mounted and stationary', async ({ page }, testInfo) => {
  test.skip(!testInfo.project.name.includes('desktop'), 'Desktop screenshot coverage');
  const browserErrors = collectBrowserErrors(page);

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/projects/my-app');
  await expectCompactParameters(page);
  const table = page.getByTestId('parameter-table');
  await table.evaluate(element => element.setAttribute('data-visual-identity', 'retained-list'));
  const tableWidthBefore = (await table.boundingBox())?.width;
  const editButton = page.getByTestId('parameter-edit-Payments:ProviderTimeoutMs');
  await editButton.scrollIntoViewIfNeeded();
  const scrollBefore = await page.evaluate(() => window.scrollY);

  await editButton.click();

  const panel = page.getByTestId('parameter-side-panel');
  await expect(panel).toBeVisible();
  await expect(panel).toHaveAttribute('data-mode', 'live');
  await expect(table).toHaveAttribute('data-visual-identity', 'retained-list');
  expect((await table.boundingBox())?.width).toBe(tableWidthBefore);
  expect(await page.evaluate(() => window.scrollY)).toBe(scrollBefore);
  await expectPanelDockedRight(page, panel);
  await expect(panel.getByRole('heading', { name: 'ProviderTimeoutMs' })).toBeVisible();
  await expect(
    page.getByTestId('parameter-panel-settings').getByText('Payments:ProviderTimeoutMs', {
      exact: true,
    }),
  ).toBeVisible();
  await expect(panel.getByText('production', { exact: true })).toBeVisible();
  await expect(panel.getByText('number', { exact: true }).first()).toBeVisible();
  await expect(panel.getByText('server', { exact: true }).first()).toBeVisible();
  await expect(page.getByTestId('parameter-edit-description-input')).toHaveValue(
    'Maximum payment provider response time.',
  );
  await expect(page.getByTestId('parameter-edit-unit-input')).toHaveValue('ms');
  await expect(page.getByTestId('parameter-edit-value-input')).toHaveValue('2500');
  await expect(page.getByTestId('parameter-panel-share-button')).toBeVisible();
  await expect(page.getByTestId('parameter-panel-settings-tab')).toHaveAttribute('aria-selected', 'true');
  await expect(page.getByTestId('parameter-panel-history-tab')).toBeVisible();
  await expect(page.getByTestId('parameter-edit-save-button')).toBeVisible();
  await expect(panel.getByRole('button', { name: 'Cancel' })).toBeVisible();
  await expect(panel.locator('footer')).toBeVisible();

  await expect(page).toHaveScreenshot('parameter-settings-compact.png');
  await expectNoHorizontalOverflow(page);
  expect(browserErrors).toEqual([]);
});

test('invalid inline JSON remains visible with its parser error', async ({ page }, testInfo) => {
  test.skip(!testInfo.project.name.includes('desktop'), 'Desktop screenshot coverage');
  const browserErrors = collectBrowserErrors(page);

  await page.goto('/projects/my-app');
  await expectCompactParameters(page);
  const input = page.getByTestId('parameter-value-input-Features:Search:Rules');
  await input.fill('{"minScore":');
  await expect(input).toHaveValue('{"minScore":');
  await expect(input).toHaveAttribute('aria-invalid', 'true');
  const error = page.getByRole('alert');
  await expect(error).toContainText('Invalid JSON:');
  await expect(error).not.toHaveText('Invalid JSON:');
  await expect(page.getByTestId('parameter-update-Features:Search:Rules')).toBeDisabled();

  const row = page.getByTestId('parameter-row-Features:Search:Rules');
  await expect(row).toHaveScreenshot('parameter-json-invalid.png');
  await expectNoHorizontalOverflow(page);
  expect(browserErrors).toEqual([]);
});

test('live history marks the current version and offers restore', async ({ page }, testInfo) => {
  test.skip(!testInfo.project.name.includes('desktop'), 'Desktop screenshot coverage');
  const browserErrors = collectBrowserErrors(page);
  let historyRequests = 0;
  page.on('request', request => {
    if (decodeURIComponent(new URL(request.url()).pathname).endsWith('/history')) historyRequests += 1;
  });

  await page.goto('/projects/my-app');
  await expectCompactParameters(page);
  await page.getByTestId('parameter-edit-Checkout:FreeShippingThreshold').click();
  expect(historyRequests).toBe(0);
  await page.getByTestId('parameter-panel-history-tab').click();
  await expect.poll(() => historyRequests).toBe(1);

  const panel = page.getByTestId('parameter-side-panel');
  await expect(panel.getByText('In use')).toBeVisible();
  await expect(panel.getByText('Changed by alice@example.com')).toBeVisible();
  await expect(panel.getByText('Changed by bob@example.com')).toBeVisible();
  await expect(panel.getByRole('button', { name: 'Restore' })).toBeVisible();
  await expect(panel).toHaveScreenshot('parameter-history.png');
  await expectNoHorizontalOverflow(page);
  expect(browserErrors).toEqual([]);
});

test('release snapshot presents the compact tree read-only', async ({ page }, testInfo) => {
  test.skip(!testInfo.project.name.includes('desktop'), 'Desktop screenshot coverage');
  const browserErrors = collectBrowserErrors(page);

  await page.goto('/projects/my-app?viewRelease=2.0.0');
  await expect(page.getByTestId('release-view-banner')).toContainText('Viewing release 2.0.0');
  await expectCompactParameters(page);
  await expect(page.getByRole('link', { name: 'Releases' })).toHaveAttribute('aria-current', 'page');
  await expect(page.getByRole('link', { name: 'Parameters' })).not.toHaveAttribute('aria-current');
  await expect(page.getByTestId('parameter-value-Checkout:FreeShippingThreshold')).toHaveText(
    /25\s*USD/,
  );
  await expect(page.getByTestId('parameter-update-Checkout:FreeShippingThreshold')).toHaveCount(0);
  await expect(page.getByTestId('parameter-delete-Checkout:FreeShippingThreshold')).toHaveCount(0);
  await expect(page.getByTestId('parameter-edit-Checkout:FreeShippingThreshold')).toHaveAccessibleName(
    'View parameter Checkout:FreeShippingThreshold',
  );

  await expect(page).toHaveScreenshot('parameter-release-snapshot.png', { fullPage: true });
  await expectNoHorizontalOverflow(page);
  expect(browserErrors).toEqual([]);
});

test('amend history offers Use in draft', async ({ page }, testInfo) => {
  test.skip(!testInfo.project.name.includes('desktop'), 'Desktop screenshot coverage');
  const browserErrors = collectBrowserErrors(page);

  await page.goto('/projects/my-app?release=2.0.1&amend=2.0.0');
  await expect(page.getByTestId('release-amend-panel')).toBeVisible();
  await expectCompactParameters(page);
  await expect(page.getByRole('link', { name: 'Releases' })).toHaveAttribute('aria-current', 'page');
  await page.getByTestId('parameter-edit-Checkout:FreeShippingThreshold').click();
  await page.getByTestId('parameter-panel-history-tab').click();

  const panel = page.getByTestId('parameter-side-panel');
  await expect(panel).toHaveAttribute('data-mode', 'amend');
  await expect(panel.getByRole('button', { name: 'Use in draft' }).first()).toBeVisible();
  await expect(panel.getByRole('button', { name: 'Restore' })).toHaveCount(0);
  await expect(page).toHaveScreenshot('parameter-amend-history.png');
  await expectNoHorizontalOverflow(page);
  expect(browserErrors).toEqual([]);
});

test('mobile create panel occupies the full screen', async ({ page }, testInfo) => {
  test.skip(!testInfo.project.name.includes('mobile'), 'Mobile screenshot coverage');
  const browserErrors = collectBrowserErrors(page);

  await page.goto('/projects/my-app');
  await expectCompactParameters(page);
  await page.getByTestId('project-add-parameter-button').click();

  const panel = page.getByTestId('parameter-side-panel');
  await expect(panel).toHaveAttribute('data-mode', 'create');
  await expect(page.getByTestId('parameter-panel-back-button')).toBeVisible();
  await expect(page.getByTestId('parameter-panel-close-button')).toBeVisible();
  await expect(page.getByTestId('parameter-key-input')).toBeVisible();
  await expect(page.getByTestId('parameter-edit-description-input')).toBeVisible();
  await expect(panel.getByRole('button', { name: 'Datatype text' })).toBeVisible();
  await expect(panel.getByRole('button', { name: 'Scope All' })).toBeVisible();
  await expect(page.getByTestId('parameter-value-input')).toBeVisible();
  await expect(page.getByTestId('parameter-create-submit-button')).toBeVisible();
  await expect(panel.getByRole('button', { name: 'Cancel' })).toBeVisible();
  await expectFullScreen(page, panel);
  await expectPanelAtTop(panel);
  await expectMobilePanelHeader(panel);

  await expect(page).toHaveScreenshot('parameter-mobile-create.png');
  await expectNoHorizontalOverflow(page);
  expect(browserErrors).toEqual([]);
});

test('mobile edit panel occupies the full screen', async ({ page }, testInfo) => {
  test.skip(!testInfo.project.name.includes('mobile'), 'Mobile screenshot coverage');
  const browserErrors = collectBrowserErrors(page);

  await page.goto('/projects/my-app');
  await expectCompactParameters(page);
  await page.getByTestId('parameter-edit-Checkout:FreeShippingThreshold').click();

  const panel = page.getByTestId('parameter-side-panel');
  await expect(panel).toHaveAttribute('data-mode', 'live');
  await expect(page.getByTestId('parameter-panel-back-button')).toBeVisible();
  await expect(page.getByTestId('parameter-panel-close-button')).toBeVisible();
  await expect(page.getByTestId('parameter-panel-share-button')).toBeVisible();
  await expect(page.getByTestId('parameter-panel-settings-tab')).toBeVisible();
  await expect(page.getByTestId('parameter-panel-history-tab')).toBeVisible();
  await expect(page.getByTestId('parameter-edit-description-input')).toHaveValue(
    'Minimum cart value for free shipping.',
  );
  await expect(page.getByTestId('parameter-edit-unit-input')).toHaveValue('USD');
  await expect(page.getByTestId('parameter-edit-save-button')).toBeVisible();
  await expect(panel.getByRole('button', { name: 'Cancel' })).toBeVisible();
  await expectFullScreen(page, panel);
  await expectPanelAtTop(panel);
  await expectMobilePanelHeader(panel);

  await expect(page).toHaveScreenshot('parameter-mobile-edit.png');
  await expectNoHorizontalOverflow(page);
  expect(browserErrors).toEqual([]);
});

async function signIn(page: Page) {
  await page.addInitScript(() => {
    localStorage.setItem('auth_token', 'visual-test-token');
    localStorage.setItem(
      'auth_session',
      JSON.stringify({ email: 'admin@example.com', role: 'admin' }),
    );
    localStorage.setItem('sidebar_collapsed', 'true');
    localStorage.setItem('nona_parameter_density', JSON.stringify('comfortable'));
  });
}

async function mockApi(page: Page) {
  await page.route('**/admin/**', async route => {
    const request = route.request();
    const path = decodeURIComponent(new URL(request.url()).pathname);

    if (request.method() === 'GET' && path === '/admin/projects') {
      await route.fulfill({ json: [project] });
      return;
    }

    if (request.method() === 'GET' && path === '/admin/users') {
      await route.fulfill({ json: [] });
      return;
    }

    if (request.method() === 'GET' && path === '/admin/projects/my-app/environments') {
      await route.fulfill({ json: environments });
      return;
    }

    if (request.method() === 'GET' && path === '/admin/projects/my-app/api-keys') {
      await route.fulfill({ json: [] });
      return;
    }

    if (
      request.method() === 'GET'
      && path === '/admin/projects/my-app/environments/production/config-entries'
    ) {
      await route.fulfill({ json: configEntries });
      return;
    }

    if (
      request.method() === 'GET'
      && path.endsWith('/config-entries/Checkout:FreeShippingThreshold/history')
    ) {
      await route.fulfill({ json: history });
      return;
    }

    if (
      request.method() === 'GET'
      && path === '/admin/projects/my-app/environments/production/releases'
    ) {
      await route.fulfill({ json: releases });
      return;
    }

    if (
      request.method() === 'GET'
      && path === '/admin/projects/my-app/environments/production/releases/2.0.0'
    ) {
      await route.fulfill({
        json: {
          ...releases[0],
          entries: configEntries.map(({ key, value, contentType, scope, description, unit }) => ({
            key,
            value,
            contentType,
            scope,
            description,
            unit,
          })),
        },
      });
      return;
    }

    await route.fulfill({ status: 404, json: { error: `Unhandled visual test route: ${path}` } });
  });
}

async function expectCompactParameters(page: Page) {
  await expect(page.getByTestId('parameter-table')).toHaveAttribute('data-density', 'compact');
  await expect(page.getByRole('group', { name: 'Parameter spacing' })).toHaveCount(0);
  await expect
    .poll(() => page.evaluate(() => localStorage.getItem('nona_parameter_density')))
    .toBeNull();
}

function collectBrowserErrors(page: Page) {
  const errors: string[] = [];
  page.on('console', message => {
    if (message.type() === 'error') errors.push(message.text());
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

async function expectPanelDockedRight(page: Page, panel: Locator) {
  const viewport = page.viewportSize();
  const box = await panel.boundingBox();
  expect(viewport).not.toBeNull();
  expect(box).not.toBeNull();
  expect(Math.round((box?.x ?? 0) + (box?.width ?? 0))).toBe(viewport?.width);
}

async function expectFullScreen(page: Page, panel: Locator) {
  const viewport = page.viewportSize();
  const box = await panel.boundingBox();
  expect(viewport).not.toBeNull();
  expect(box).not.toBeNull();
  expect(box?.x).toBe(0);
  expect(box?.y).toBe(0);
  expect(box?.width).toBe(viewport?.width);
  expect(box?.height).toBe(viewport?.height);
}

async function expectPanelAtTop(panel: Locator) {
  await expect.poll(() => panel.locator('main').evaluate(element => element.scrollTop)).toBe(0);
}

async function expectMobilePanelHeader(panel: Locator) {
  const positions = await panel.locator('header').evaluate(header => {
    const [back, title, actions] = [...header.children] as HTMLElement[];
    const close = actions.lastElementChild as HTMLElement;
    const panelBox = header.parentElement?.getBoundingClientRect();
    const headerBox = header.getBoundingClientRect();
    return {
      panelLeft: panelBox?.left ?? 0,
      panelRight: panelBox?.right ?? 0,
      headerLeft: headerBox.left,
      headerRight: headerBox.right,
      backLeft: back.getBoundingClientRect().left,
      backRight: back.getBoundingClientRect().right,
      titleLeft: title.getBoundingClientRect().left,
      titleRight: title.getBoundingClientRect().right,
      actionsLeft: actions.getBoundingClientRect().left,
      actionsRight: actions.getBoundingClientRect().right,
      closeLeft: close.getBoundingClientRect().left,
      closeRight: close.getBoundingClientRect().right,
    };
  });

  expect(positions.headerLeft).toBeGreaterThanOrEqual(positions.panelLeft);
  expect(positions.headerRight).toBeLessThanOrEqual(positions.panelRight);
  expect(positions.backLeft).toBeGreaterThanOrEqual(positions.headerLeft);
  expect(positions.backRight).toBeLessThanOrEqual(positions.titleLeft);
  expect(positions.titleRight).toBeLessThanOrEqual(positions.actionsLeft);
  expect(positions.actionsRight).toBeLessThanOrEqual(positions.headerRight);
  expect(positions.closeLeft).toBeGreaterThanOrEqual(positions.headerLeft);
  expect(positions.closeRight).toBeLessThanOrEqual(positions.headerRight);
}
