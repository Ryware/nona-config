import { expect, test } from "@playwright/test";
import { mockAdminApi, signIn } from "./support/mock-api";

test.beforeEach(async ({ page }) => {
  await signIn(page);
  await mockAdminApi(page);
});

test("team list supports search and member removal confirmation", async ({ page }) => {
  await page.goto("/users");

  await expect(page.getByTestId("team-heading")).toBeVisible();
  await expect(page.getByTestId("team-row-user-1")).toBeVisible();
  await expect(page.getByTestId("team-row-user-2")).toBeVisible();

  await page.getByTestId("team-search-input").fill("viewer");
  await expect(page.getByTestId("team-row-user-3")).toBeVisible();
  await expect(page.getByTestId("team-row-user-1")).toBeHidden();

  await page.getByTestId("team-search-input").fill("");
  await page.getByTestId("team-remove-user-2").click();
  await expect(page.getByTestId("remove-member-dialog")).toBeVisible();
  await page.getByTestId("remove-member-confirm-button").click();
  await expect(page.getByTestId("team-row-user-2")).toBeHidden();
});

test("member invitation generates a shareable invite link", async ({ page }) => {
  await page.goto("/user");

  await expect(page.getByTestId("invite-heading")).toBeVisible();
  await page.getByTestId("invite-name-input").fill("Priya Product");
  await page.getByTestId("invite-email-input").fill("priya@example.com");
  await page.getByTestId("invite-role-viewer").click();
  await page.getByTestId("invite-project-my-app").check();
  await page.getByTestId("invite-submit-button").click();

  await expect(page.getByTestId("invite-link-heading")).toBeVisible();
  await expect(page.getByTestId("invite-link-input")).toHaveValue(/\/invite\/invite-token-123$/);
});
