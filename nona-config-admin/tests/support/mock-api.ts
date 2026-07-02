import type { Page, Route } from "@playwright/test";

type Project = {
  id: string;
  urlSlug: string;
  name: string;
  description?: string;
  environments: string[];
  createdAt: string;
  updatedAt: string;
};

type ApiKey = {
  id: string;
  name: string;
  key: string;
  project: string;
  environment: string | null;
  scope: "client" | "server" | "all";
  createdAt: string;
  updatedAt: string;
};

type ConfigEntry = {
  project: string;
  environment: string;
  key: string;
  value: string;
  contentType: "text" | "number" | "boolean" | "json";
  scope: "client" | "server" | "all";
  activeVersion: number;
  createdAt: string;
  updatedAt: string;
};

type ConfigEntryVersion = {
  project: string;
  environment: string;
  key: string;
  version: number;
  value: string;
  contentType: "text" | "number" | "boolean" | "json";
  scope: "client" | "server" | "all";
  createdAt: string;
  actor: string;
};

type User = {
  id: string;
  email: string;
  isAdmin: boolean;
  role: string;
  name: string;
  projects?: { projectName: string; role: string }[];
  createdAt: string;
  updatedAt?: string;
};

type AuditLog = {
  id: string;
  actor: string;
  actorIsSystem: boolean;
  action: string;
  target: string;
  project: string | null;
  environment: string | null;
  createdAt: string;
};

const now = "2026-06-07T03:00:00Z";

export const testProjects: Project[] = [
  {
    id: "proj-1",
    urlSlug: "my-app",
    name: "my-app",
    description: "Main application config",
    environments: ["production", "staging"],
    createdAt: "2024-01-01T00:00:00Z",
    updatedAt: now
  },
  {
    id: "proj-2",
    urlSlug: "billing-api",
    name: "billing-api",
    description: "Billing service flags",
    environments: ["production"],
    createdAt: "2024-02-01T00:00:00Z",
    updatedAt: "2026-06-06T12:00:00Z"
  }
];

const testUsers: User[] = [
  {
    id: "user-1",
    email: "admin@example.com",
    isAdmin: true,
    role: "admin",
    name: "Avery Admin",
    projects: [],
    createdAt: "2024-01-01T00:00:00Z"
  },
  {
    id: "user-2",
    email: "editor@example.com",
    isAdmin: false,
    role: "editor",
    name: "Evan Editor",
    projects: [{ projectName: "my-app", role: "editor" }],
    createdAt: "2024-01-02T00:00:00Z"
  },
  {
    id: "user-3",
    email: "viewer@example.com",
    isAdmin: false,
    role: "viewer",
    name: "Vera Viewer",
    projects: [{ projectName: "billing-api", role: "viewer" }],
    createdAt: "2024-01-03T00:00:00Z"
  }
];

const auditLogs: AuditLog[] = [
  {
    id: "audit-1",
    actor: "admin@example.com",
    actorIsSystem: false,
    action: "created_project",
    target: "my-app",
    project: "my-app",
    environment: null,
    createdAt: "2026-06-07T02:35:00Z"
  },
  {
    id: "audit-2",
    actor: "editor@example.com",
    actorIsSystem: false,
    action: "updated_config_entry",
    target: "API_URL",
    project: "my-app",
    environment: "production",
    createdAt: "2026-06-07T02:40:00Z"
  },
  {
    id: "audit-3",
    actor: "system",
    actorIsSystem: true,
    action: "auto_scaling",
    target: "worker-pool",
    project: "billing-api",
    environment: "staging",
    createdAt: "2026-06-07T02:45:00Z"
  }
];

function slugifyProjectName(name: string) {
  return name.trim().toLowerCase().replace(/\s+/g, "-");
}

function response(route: Route, body: unknown, status = 200) {
  return route.fulfill({
    status,
    contentType: "application/json",
    body: JSON.stringify(body)
  });
}

function noContent(route: Route) {
  return route.fulfill({ status: 204 });
}

export async function signIn(page: Page) {
  await page.addInitScript(() => {
    localStorage.setItem("auth_token", "visual-test-token");
    localStorage.setItem(
      "auth_session",
      JSON.stringify({ email: "admin@example.com", role: "Admin" })
    );
  });
}

export async function mockAdminApi(page: Page) {
  let projects = testProjects.map(project => ({ ...project }));
  let users = testUsers.map(user => ({
    ...user,
    projects: user.projects ? [...user.projects] : []
  }));
  let configEntries: ConfigEntry[] = [
    {
      project: "my-app",
      environment: "production",
      key: "API_URL",
      value: "https://api.example.com",
      contentType: "text",
      scope: "all",
      activeVersion: 1,
      createdAt: "2024-01-01T00:00:00Z",
      updatedAt: "2024-01-01T00:00:00Z"
    }
  ];
  let configEntryVersions: ConfigEntryVersion[] = [
    {
      project: "my-app",
      environment: "production",
      key: "API_URL",
      version: 1,
      value: "https://api.example.com",
      contentType: "text",
      scope: "all",
      createdAt: "2024-01-01T00:00:00Z",
      actor: "admin@example.com"
    }
  ];
  let apiKeys: ApiKey[] = [
    {
      id: "key-1",
      name: "Web Client",
      key: "key_" + "A".repeat(24),
      project: "my-app",
      environment: "production",
      scope: "client",
      createdAt: "2024-01-01T00:00:00Z",
      updatedAt: "2024-01-01T00:00:00Z"
    },
    {
      id: "key-2",
      name: "Backend",
      key: "key_" + "B".repeat(24),
      project: "my-app",
      environment: null,
      scope: "server",
      createdAt: "2024-01-02T00:00:00Z",
      updatedAt: "2024-01-02T00:00:00Z"
    }
  ];

  await page.route(/\/auth\//, async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;

    if (request.method() === "GET" && path === "/auth/first-time") {
      await response(route, false);
      return;
    }

    if (request.method() === "GET" && path === "/auth/sso/config") {
      await response(route, { google: { enabled: false }, microsoft: { enabled: false } });
      return;
    }

    if (request.method() === "POST" && path === "/auth/login") {
      await response(route, {
        token: "visual-test-token",
        username: "admin@example.com",
        role: "Admin"
      });
      return;
    }

    if (request.method() === "POST" && path === "/auth/register") {
      await response(route, {
        success: true,
        response: {
          token: "visual-test-token",
          username: "root@example.com",
          role: "Admin"
        },
        error: null
      });
      return;
    }

    if (request.method() === "POST" && path === "/auth/forgot-password") {
      await noContent(route);
      return;
    }

    await response(route, { error: "Auth route not mocked" }, 404);
  });

  await page.route(/\/admin\//, async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    const method = request.method();

    if (method === "GET" && path === "/admin/dashboard/counts") {
      await response(route, {
        projects: projects.length,
        configEntries: configEntries.length,
        users: users.length
      });
      return;
    }

    if (method === "GET" && path === "/admin/projects") {
      await response(route, projects);
      return;
    }

    if (method === "POST" && path === "/admin/projects") {
      const body = JSON.parse(request.postData() || "{}") as { name: string; description?: string };
      const slug = slugifyProjectName(body.name);
      const project: Project = {
        id: `proj-${projects.length + 1}`,
        urlSlug: slug,
        name: body.name,
        description: body.description,
        environments: ["production"],
        createdAt: now,
        updatedAt: now
      };
      projects = [...projects, project];
      await response(route, project, 201);
      return;
    }

    const projectMatch = path.match(/^\/admin\/projects\/([^/]+)$/);
    if (method === "DELETE" && projectMatch) {
      const projectName = decodeURIComponent(projectMatch[1]);
      projects = projects.filter(
        project => project.name !== projectName && project.urlSlug !== projectName
      );
      await noContent(route);
      return;
    }

    if (method === "GET" && path === "/admin/projects/my-app/api-keys") {
      await response(route, apiKeys.filter(apiKey => apiKey.project === "my-app"));
      return;
    }

    if (method === "POST" && path === "/admin/projects/my-app/api-keys") {
      const body = JSON.parse(request.postData() || "{}") as {
        name?: string;
        environment?: string | null;
        scope?: "client" | "server" | "all";
      };
      const apiKey: ApiKey = {
        id: `key-${apiKeys.length + 1}`,
        name: body.name ?? "Untitled key",
        key: "key_" + "N".repeat(24),
        project: "my-app",
        environment: body.environment ?? null,
        scope: body.scope ?? "client",
        createdAt: now,
        updatedAt: now
      };
      apiKeys = [...apiKeys, apiKey];
      await response(route, apiKey, 201);
      return;
    }

    const apiKeyMatch = path.match(/^\/admin\/projects\/my-app\/api-keys\/([^/]+)$/);
    if (method === "DELETE" && apiKeyMatch) {
      const apiKeyId = decodeURIComponent(apiKeyMatch[1]);
      apiKeys = apiKeys.filter(apiKey => apiKey.id !== apiKeyId);
      await noContent(route);
      return;
    }

    if (method === "GET" && path === "/admin/projects/my-app/environments") {
      await response(route, [
        {
          project: "my-app",
          name: "production",
          createdAt: "2024-01-01T00:00:00Z",
          updatedAt: now
        },
        { project: "my-app", name: "staging", createdAt: "2024-01-01T00:00:00Z", updatedAt: now }
      ]);
      return;
    }

    if (
      method === "GET" &&
      path === "/admin/projects/my-app/environments/production/config-entries"
    ) {
      await response(route, configEntries);
      return;
    }

    const configHistoryMatch = path.match(
      /^\/admin\/projects\/my-app\/environments\/production\/config-entries\/([^/]+)\/history$/
    );
    if (method === "GET" && configHistoryMatch) {
      const key = decodeURIComponent(configHistoryMatch[1]);
      await response(
        route,
        configEntryVersions
          .filter(version => version.key === key)
          .sort((a, b) => b.version - a.version)
      );
      return;
    }

    const configRollbackMatch = path.match(
      /^\/admin\/projects\/my-app\/environments\/production\/config-entries\/([^/]+)\/rollback$/
    );
    if (method === "POST" && configRollbackMatch) {
      const key = decodeURIComponent(configRollbackMatch[1]);
      const body = JSON.parse(request.postData() || "{}") as { version?: number };
      const existing = configEntries.find(entry => entry.key === key);
      const target = configEntryVersions.find(
        version => version.key === key && version.version === body.version
      );
      if (!existing || !target) {
        await response(route, { error: "Version not found" }, 404);
        return;
      }

      const entryVersions = configEntryVersions.filter(version => version.key === key);
      const nextVersion = Math.max(...entryVersions.map(version => version.version), 0) + 1;
      const entry: ConfigEntry = {
        ...existing,
        value: target.value,
        contentType: target.contentType,
        scope: target.scope,
        activeVersion: nextVersion,
        updatedAt: now
      };
      const version: ConfigEntryVersion = {
        project: entry.project,
        environment: entry.environment,
        key: entry.key,
        version: nextVersion,
        value: entry.value,
        contentType: entry.contentType,
        scope: entry.scope,
        createdAt: now,
        actor: "admin@example.com"
      };
      configEntries = configEntries.map(item => (item.key === key ? entry : item));
      configEntryVersions = [...configEntryVersions, version];
      await response(route, entry);
      return;
    }

    const configMatch = path.match(
      /^\/admin\/projects\/my-app\/environments\/production\/config-entries\/([^/]+)$/
    );
    if (configMatch) {
      const key = decodeURIComponent(configMatch[1]);

      if (method === "PUT") {
        const body = JSON.parse(request.postData() || "{}") as Partial<ConfigEntry>;
        const existing = configEntries.find(entry => entry.key === key);
        const entryVersions = configEntryVersions.filter(version => version.key === key);
        const nextVersion = entryVersions.length === 0
          ? 1
          : Math.max(...entryVersions.map(version => version.version)) + 1;
        const entry: ConfigEntry = {
          project: "my-app",
          environment: "production",
          key,
          value: String(body.value ?? existing?.value ?? ""),
          contentType: body.contentType ?? existing?.contentType ?? "text",
          scope: body.scope ?? existing?.scope ?? "all",
          activeVersion: nextVersion,
          createdAt: existing?.createdAt ?? now,
          updatedAt: now
        };
        const version: ConfigEntryVersion = {
          project: entry.project,
          environment: entry.environment,
          key: entry.key,
          version: nextVersion,
          value: entry.value,
          contentType: entry.contentType,
          scope: entry.scope,
          createdAt: now,
          actor: "admin@example.com"
        };
        configEntries = existing
          ? configEntries.map(item => (item.key === key ? entry : item))
          : [...configEntries, entry];
        configEntryVersions = [...configEntryVersions, version];
        await response(route, entry);
        return;
      }

      if (method === "DELETE") {
        configEntries = configEntries.filter(entry => entry.key !== key);
        configEntryVersions = configEntryVersions.filter(version => version.key !== key);
        await noContent(route);
        return;
      }
    }

    if (method === "GET" && path === "/admin/users") {
      await response(route, users);
      return;
    }

    if (method === "POST" && path === "/admin/users") {
      const body = JSON.parse(request.postData() || "{}") as {
        name: string;
        email: string;
        role?: string;
      };
      const user: User = {
        id: `user-${users.length + 1}`,
        email: body.email,
        isAdmin: false,
        role: body.role ?? "viewer",
        name: body.name,
        projects: [],
        createdAt: now
      };
      users = [...users, user];
      await response(route, { user, invitationToken: "invite-token-123" }, 201);
      return;
    }

    const userProjectMatch = path.match(/^\/admin\/users\/([^/]+)\/projects\/([^/]+)$/);
    if (method === "PUT" && userProjectMatch) {
      await response(route, {
        projectName: decodeURIComponent(userProjectMatch[2]),
        role: "editor"
      });
      return;
    }

    const userMatch = path.match(/^\/admin\/users\/([^/]+)$/);
    if (method === "DELETE" && userMatch) {
      const id = decodeURIComponent(userMatch[1]);
      users = users.filter(user => user.id !== id);
      await noContent(route);
      return;
    }

    if (method === "GET" && path === "/admin/audit-logs") {
      await response(route, auditLogs);
      return;
    }

    await response(route, { error: `Admin route not mocked: ${method} ${path}` }, 404);
  });
}
