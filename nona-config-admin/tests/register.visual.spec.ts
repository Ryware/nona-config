import { expect, test } from "@playwright/test";
import { mockAdminApi } from "./support/mock-api";

test.beforeEach(async ({ page }) => {
  await mockAdminApi(page);
});

test("admin registration creates the root account and opens projects", async ({ page }) => {
  await page.goto("/register");

  await expect(page.getByTestId("register-heading")).toBeVisible();
  await page.getByTestId("register-email-input").fill("root@example.com");
  await page.getByTestId("register-password-input").fill("CorrectHorseBatteryStaple!42");
  await page.getByTestId("register-confirm-password-input").fill("CorrectHorseBatteryStaple!42");
  await page.getByTestId("register-submit-button").click();

  await expect(page).toHaveURL(/\/projects$/);
  await expect(page.getByTestId("projects-heading")).toBeVisible();
  await expect(page.getByTestId("project-card-my-app")).toBeVisible();
});
