import { expect, test } from "@playwright/test";
import { mockAdminApi, signIn } from "./support/mock-api";

test.beforeEach(async ({ page }) => {
  await signIn(page);
  await mockAdminApi(page);
});

test("project parameter create, edit, and delete flow is usable", async ({ page }) => {
  await page.goto("/projects/my-app");

  await expect(page.getByTestId("project-detail-heading")).toBeVisible();
  await expect(page.getByTestId("parameter-key-API_URL")).toBeVisible();

  await page.getByTestId("project-add-parameter-button").click();
  await page.getByTestId("parameter-key-input").fill("WELCOME_MESSAGE");
  await page.getByTestId("parameter-display-name-input").fill("Welcome Message");
  await page
    .getByTestId("parameter-description-input")
    .fill("Marketing headline shown on the public app.");
  await page.getByTestId("parameter-value-input").fill("Hello visual QA");
  await page.getByTestId("parameter-create-submit-button").click();

  await expect(page.getByTestId("parameter-key-WELCOME_MESSAGE")).toBeVisible();
  await expect(page.getByTestId("parameter-value-WELCOME_MESSAGE")).toContainText(
    "Hello visual QA"
  );

  await page.getByTestId("parameter-key-WELCOME_MESSAGE").click();
  await expect(page.getByTestId("parameter-edit-heading")).toBeVisible();
  await page.getByTestId("parameter-edit-display-name-input").fill("Homepage Welcome Message");
  await page.getByTestId("parameter-edit-value-input").fill("Hello from Playwright");
  await page.getByTestId("parameter-edit-save-button").click();

  await expect(page.getByTestId("parameter-edit-drawer")).toBeHidden();
  await expect(page.getByTestId("parameter-value-WELCOME_MESSAGE")).toContainText(
    "Hello from Playwright"
  );

  await page.getByTestId("parameter-delete-WELCOME_MESSAGE").click();
  await expect(page.getByTestId("delete-parameter-dialog")).toBeVisible();
  await page.getByTestId("delete-parameter-confirm-button").click();
  await expect(page.getByTestId("parameter-key-WELCOME_MESSAGE")).toBeHidden();
});
