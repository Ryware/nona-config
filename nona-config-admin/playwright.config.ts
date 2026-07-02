import { defineConfig, devices } from "@playwright/test";

const desktopViewport = { width: 1920, height: 1080 };
const mobileViewport = { width: 390, height: 844 };

export default defineConfig({
  testDir: "./tests",
  testMatch: "**/*.visual.spec.ts",
  fullyParallel: true,
  reporter: process.env.CI ? "github" : "list",
  use: {
    baseURL: "http://127.0.0.1:4174",
    trace: "on-first-retry",
    screenshot: "only-on-failure"
  },
  webServer: {
    command: "npm run build && npm run preview -- --host 127.0.0.1 --port 4174",
    url: "http://127.0.0.1:4174",
    reuseExistingServer: !process.env.CI,
    timeout: 120_000
  },
  projects: [
    {
      name: "desktop-chromium",
      use: {
        ...devices["Desktop Chrome"],
        viewport: desktopViewport,
        video: {
          mode: "on",
          size: desktopViewport
        }
      }
    },
    {
      name: "mobile-chromium",
      use: {
        ...devices["Pixel 5"],
        viewport: mobileViewport,
        video: {
          mode: "on",
          size: mobileViewport
        }
      }
    }
  ]
});
