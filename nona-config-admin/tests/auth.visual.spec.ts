import { expect, test } from "@playwright/test";
import { mockAdminApi } from "./support/mock-api";

test.beforeEach(async ({ page }) => {
  await mockAdminApi(page);
});

test("email login opens the projects workspace", async ({ page }) => {
  await page.goto("/login");

  await expect(page.getByTestId("login-heading")).toBeVisible();
  await page.getByTestId("login-email-input").fill("admin@example.com");
  await page.getByTestId("login-password-input").fill("correct horse battery staple");
  await page.getByTestId("login-submit-button").click();

  await expect(page).toHaveURL(/\/projects$/);
  await expect(page.getByTestId("projects-heading")).toBeVisible();
  await expect(page.getByTestId("project-card-my-app")).toBeVisible();
});

test("forgot password shows the sent state", async ({ page }) => {
  await page.goto("/login");

  await page.getByTestId("forgot-open-button").click();
  await expect(page.getByTestId("forgot-heading")).toBeVisible();
  await page.getByTestId("forgot-email-input").fill("admin@example.com");
  await page.getByTestId("forgot-submit-button").click();

  await expect(page.getByTestId("forgot-sent-state")).toContainText("admin@example.com");
  await expect(page.getByTestId("forgot-back-to-login-button")).toBeVisible();
});
