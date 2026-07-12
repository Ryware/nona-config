---
title: Parameter share links
description: Create temporary links that let someone view or edit one config parameter.
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

## Create a link with the CLI

```bash
nona entries share create \
  --environment production \
  --key Features:Checkout \
  --expiration 1h
```

Create a view-only link:

```bash
nona entries share create \
  --environment production \
  --key Features:Checkout \
  --expiration 3d \
  --view-only
```

The CLI prints the token and browser link. If the public admin app is not hosted on the same origin as the API URL, pass the browser origin:

```bash
nona entries share create \
  --environment production \
  --key Features:Checkout \
  --share-base-url https://admin.nona.example.com
```

## Manage existing links

List links for a parameter:

```bash
nona entries share list \
  --environment production \
  --key Features:Checkout
```

Admin API list responses include each link token, so the admin UI can copy existing links.

Revoke a link:

```bash
nona entries share revoke \
  --environment production \
  --key Features:Checkout \
  --id 11
```

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
