import { expect, test } from "@playwright/test";

const initialProject = {
  id: "proj-1",
  urlSlug: "my-app",
  name: "my-app",
  description: "Main application config",
  environments: ["production", "staging"],
  createdAt: "2024-01-01T00:00:00Z",
  updatedAt: "2024-01-01T00:00:00Z"
};

const initialApiKeys = [
  {
    id: "key-1",
    name: "Web Client",
    key: "key_" + "A".repeat(24),
    project: "my-app",
    environment: "production",
    scope: "client",
    createdAt: "2024-01-01T00:00:00Z",
    updatedAt: "2024-01-01T00:00:00Z"
  }
];

test.beforeEach(async ({ page }) => {
  await page.addInitScript(() => {
    localStorage.setItem("auth_token", "visual-test-token");
    localStorage.setItem(
      "auth_session",
      JSON.stringify({ email: "admin@example.com", role: "Admin" })
    );
  });

  let project = { ...initialProject };
  let apiKeys = initialApiKeys.map(apiKey => ({ ...apiKey }));

  await page.route(/\/admin\/projects/, async route => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname;

    if (request.method() === "GET" && path === "/admin/projects") {
      await route.fulfill({
        contentType: "application/json",
        body: JSON.stringify([project])
      });
      return;
    }

    if (request.method() === "GET" && path === "/admin/projects/my-app/environments") {
      await route.fulfill({
        contentType: "application/json",
        body: JSON.stringify([
          {
            project: "my-app",
            name: "production",
            createdAt: "2024-01-01T00:00:00Z",
            updatedAt: "2024-01-01T00:00:00Z"
          },
          {
            project: "my-app",
            name: "staging",
            createdAt: "2024-01-01T00:00:00Z",
            updatedAt: "2024-01-01T00:00:00Z"
          }
        ])
      });
      return;
    }

    if (request.method() === "GET" && path === "/admin/projects/my-app/api-keys") {
      await route.fulfill({
        contentType: "application/json",
        body: JSON.stringify(apiKeys)
      });
      return;
    }

    if (request.method() === "POST" && path === "/admin/projects/my-app/api-keys") {
      const body = JSON.parse(request.postData() || "{}");
      const apiKey = {
        id: `key-${apiKeys.length + 1}`,
        name: body.name,
        key: "key_" + "N".repeat(24),
        project: "my-app",
        environment: body.environment ?? null,
        scope: body.scope ?? "client",
        createdAt: "2024-01-03T00:00:00Z",
        updatedAt: "2024-01-03T00:00:00Z"
      };
      apiKeys = [...apiKeys, apiKey];

      await route.fulfill({
        status: 201,
        contentType: "application/json",
        body: JSON.stringify(apiKey)
      });
      return;
    }

    if (
      request.method() === "GET" &&
      path === "/admin/projects/my-app/environments/production/config-entries"
    ) {
      await route.fulfill({
        contentType: "application/json",
        body: JSON.stringify([
          {
            project: "my-app",
            environment: "production",
            key: "API_URL",
            value: "https://api.example.com",
            contentType: "text",
            scope: "all",
            createdAt: "2024-01-01T00:00:00Z",
            updatedAt: "2024-01-01T00:00:00Z"
          }
        ])
      });
      return;
    }

    await route.fulfill({
      status: 404,
      contentType: "application/json",
      body: JSON.stringify({ error: "Not found" })
    });
  });
});

test("project API key management is visually usable", async ({ page }, testInfo) => {
  await page.goto("/projects/my-app");

  await expect(page.getByTestId("project-detail-heading")).toBeVisible();
  await expect(page.getByTestId("project-api-keys-heading")).toBeVisible();
  await expect(page.getByText("Web Client")).toBeVisible();
  await expect(page.getByTestId("environment-tab-production")).toBeVisible();
  await expect(page.getByTestId("parameter-key-API_URL")).toBeVisible();

  await page.screenshot({ path: testInfo.outputPath("api-keys-project.png"), fullPage: true });

  await page.getByTestId("api-key-toggle-key-1").click();
  await expect(page.getByTestId("api-key-value-key-1")).toContainText(initialApiKeys[0].key);

  await page.getByTestId("api-key-name-input").fill("Backend worker");
  await page.getByTestId("api-key-create-button").click();
  await expect(page.getByText("Backend worker")).toBeVisible();
});
