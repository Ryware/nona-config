import { expect, test } from "@playwright/test";
import { mockAdminApi, signIn } from "./support/mock-api";

test.beforeEach(async ({ page }) => {
  await signIn(page);
  await mockAdminApi(page);
});

test("project list supports create, search, and delete", async ({ page }) => {
  await page.goto("/projects");

  await expect(page.getByTestId("projects-heading")).toBeVisible();
  await expect(page.getByTestId("project-card-my-app")).toBeVisible();

  await page.getByTestId("projects-new-button").click();
  await page.getByTestId("project-name-input").fill("checkout-web");
  await page.getByTestId("project-description-input").fill("Checkout frontend release flags");
  await page.getByTestId("project-create-submit-button").click();

  await expect(page.getByTestId("project-card-checkout-web")).toBeVisible();

  await page.getByTestId("projects-search-input").fill("billing");
  await expect(page.getByTestId("project-card-billing-api")).toBeVisible();
  await expect(page.getByTestId("project-card-my-app")).toBeHidden();

  await page.getByTestId("projects-search-input").fill("");
  await page.getByTestId("project-delete-checkout-web").click();
  await expect(page.getByTestId("delete-project-dialog")).toBeVisible();
  await page.getByTestId("delete-project-confirm-button").click();
  await expect(page.getByTestId("project-card-checkout-web")).toBeHidden();
});
