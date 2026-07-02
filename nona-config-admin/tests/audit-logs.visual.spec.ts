import { expect, test } from "@playwright/test";
import { mockAdminApi, signIn } from "./support/mock-api";

test.beforeEach(async ({ page }) => {
  await signIn(page);
  await mockAdminApi(page);
});

test("audit logs can be searched and inspected", async ({ page }) => {
  await page.goto("/audit-logs");

  await expect(page.getByTestId("audit-logs-heading")).toBeVisible();
  await expect(page.getByTestId("audit-target-audit-2")).toBeVisible();
  await expect(page.getByTestId("audit-target-audit-3")).toBeVisible();

  await page.getByTestId("audit-search-input").fill("API_URL");
  await expect(page.getByTestId("audit-target-audit-2")).toBeVisible();
  await expect(page.getByTestId("audit-target-audit-3")).toBeHidden();

  await page.getByTestId("audit-row-audit-2").click();
  await expect(page.getByTestId("audit-drawer-heading")).toBeVisible();
  await expect(page.getByTestId("audit-drawer-metadata-heading")).toBeVisible();

  await page.getByTestId("audit-drawer-close-button").click();
  await expect(page.getByTestId("audit-drawer")).toBeHidden();
});
