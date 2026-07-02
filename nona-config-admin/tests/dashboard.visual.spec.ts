import { expect, test } from "@playwright/test";
import { mockAdminApi, signIn } from "./support/mock-api";

test.beforeEach(async ({ page }) => {
  await signIn(page);
  await mockAdminApi(page);
});

test("dashboard stats and quick actions are usable", async ({ page }) => {
  await page.goto("/dashboard");

  await expect(page.getByTestId("dashboard-heading")).toBeVisible();
  await expect(page.getByTestId("dashboard-stat-projects")).toBeVisible();
  await expect(page.getByTestId("dashboard-stat-config-entries")).toBeVisible();
  await expect(page.getByTestId("dashboard-stat-team-members")).toBeVisible();

  await page.getByTestId("dashboard-team-management-link").click();
  await expect(page).toHaveURL(/\/users$/);
  await expect(page.getByTestId("team-heading")).toBeVisible();
});
