---
title: Parameter share links
description: Create temporary, no-login links that let someone view or edit a single Nona config parameter, with expiry, revocation, and audit.
---

Parameter share links give a time-limited link to one config entry. A link can be editable or view-only, expires automatically, and can be revoked before it expires.

Use them when a teammate needs temporary access to one parameter without granting project access.

## Create a link in the admin UI

1. Open a project.
2. Open the parameter table for an environment.
3. Use the share action on the parameter row.
4. Choose an expiration and permission.
5. Copy the generated `/share/{token}` link.

Supported expirations:

- `1h`
- `1d`
- `3d`
- `30d`
- `12m`

Choose the shortest lifetime that still fits the task. In most cases, short-lived links are safer than leaving long-running access around.

The admin dialog also lets you generate a new link, copy the generated URL, review existing links for that parameter, and revoke an active link.

If you need an environment-wide view, the admin also has a dedicated **Shared Links** page inside each project that lists all share links for the currently active environment.

## Create a link with the CLI

```bash
nona entries share create \
  --project storefront \
  --environment production \
  --key Features:Checkout \
  --expiration 1h
```

Create a view-only link:

```bash
nona entries share create \
  --project storefront \
  --environment production \
  --key Features:Checkout \
  --expiration 3d \
  --view-only
```

The CLI prints the token and browser link. If the public admin app is not hosted on the same origin as the API URL, pass the browser origin:

```bash
nona entries share create \
  --project storefront \
  --environment production \
  --key Features:Checkout \
  --share-base-url https://admin.nona.example.com
```

If you already saved the default project with `nona config set project storefront`, you can omit `--project`.

## Manage existing links

List links for a parameter:

```bash
nona entries share list \
  --project storefront \
  --environment production \
  --key Features:Checkout
```

Admin API list responses include each link token, so the admin UI can copy existing links.

Revoke a link:

```bash
nona entries share revoke \
  --project storefront \
  --environment production \
  --key Features:Checkout \
  --id 11
```

In admin, revocation is handled from the same share dialog that lists the existing links.
The dedicated Shared Links page can also copy and revoke links across the active environment.

## Good use cases

Parameter share links are useful when:

- one teammate needs to review a single value
- someone outside the usual operator group needs temporary visibility
- an incident requires fast collaboration on one parameter
- editable access should be limited to one entry instead of a whole project

## Good operating pattern

Use share links when the access need is narrow, temporary, and tied to one parameter. Use normal user or project access when the person needs ongoing access to a broader part of the system.

## HTTP endpoints

Admin endpoints require an admin bearer token:

```http
GET /admin/projects/{projectId}/environments/{environmentName}/config-entries/{key}/share-links
POST /admin/projects/{projectId}/environments/{environmentName}/config-entries/{key}/share-links
DELETE /admin/projects/{projectId}/environments/{environmentName}/config-entries/{key}/share-links/{shareLinkId}
```

Public shared-parameter endpoints use the generated token:

```http
GET /public/share-links/{token}
PUT /public/share-links/{token}
```

`PUT` only succeeds for links created with edit permission.

## Security notes

- Nona stores the share-link token itself and returns it to authorized admin list requests.
- Treat share-link tokens as secrets. Anyone with the token can use the public endpoint until the link expires or is revoked.
- Expired or revoked links cannot read or update the parameter.
- Link creation and revocation are written to the audit log.

## Practical safety rules

- prefer short expirations by default
- use view-only links unless edit access is actually needed
- revoke the link once the task is done
- avoid sharing long-lived links in permanent chat history or documentation

## FAQ

### What makes parameter share links different from normal user access?

They provide narrow, temporary access to one parameter instead of broader ongoing project access.

### Should I prefer view-only links by default?

Yes.

Use view-only unless the other person truly needs edit access to that one parameter.

### Are share-link tokens sensitive?

Yes.

Anyone with the token can use the public share-link endpoint until the link expires or is revoked, so treat the token as a secret.

### When should I use a share link instead of inviting a user?

Use a share link when the access need is temporary, narrow, and limited to one parameter. Use normal user or project access for ongoing collaboration.

## Related docs

- [Users and project access](/docs/concepts/users-and-project-access/)
- [Audit logs](/docs/concepts/audit-logs/)
