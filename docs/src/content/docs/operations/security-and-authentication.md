---
title: Security and authentication
description: Secure a self-hosted Nona deployment with scoped API keys, pinned JWT settings, controlled share links, and SSO-backed admin access.
---

Nona is self-hosted, so basic security and authentication decisions belong to your deployment process, not only to the application code.

In practice, the security model has a few distinct layers:

- admin authentication for people
- API-key authentication for config reads
- project access control for what each user can change
- auditability for sensitive actions

## First production steps

For a real deployment, the first security checklist is:

1. pin JWT settings if that is your operating model
2. create individual user accounts instead of shared credentials
3. create narrow API keys for each app or service
4. keep project access limited to the teams that need it
5. use short-lived share links when temporary access is enough

## API keys

Use narrow API keys whenever possible.

Good habits:

- create separate keys for separate apps or services
- scope keys to `client`, `server`, or `all` deliberately
- scope keys to the specific environment they need when possible
- keep keys in environment variables or a secrets system

API keys protect the runtime config API. They are not replaced by SSO.

## Basic API-key workflow

In admin:

1. open the project
2. use the `API Keys` section
3. create a key per app or service
4. choose the narrowest scope that works
5. limit the key to one environment when possible

## JWT settings

Nona can generate and persist JWT settings automatically, but production deployments are easier to reason about when those values are pinned explicitly.

The deployment guides show these settings:

- `Jwt__Key`
- `Jwt__Issuer`
- `Jwt__Audience`

Keep `Jwt__Key` in a real secret store or controlled `.env` workflow for production.

If you rotate or change these values unexpectedly, treat that as a meaningful auth-impacting maintenance event.

## User authentication

The repo supports multiple admin authentication paths:

- email and password
- Google SSO
- Microsoft SSO
- invitation-based onboarding

That matters because a self-hosted admin surface should not depend on shared operator credentials.

## Good admin-access pattern

Prefer:

- one account per operator
- invitations for onboarding
- SSO where it fits your identity workflow
- project-level access instead of broad shared admin access

## Share-link tokens

Parameter share links are powerful because they allow narrow temporary access, but that also means the token itself should be treated like a secret.

Remember:

- anyone with the token can use the public share-link endpoint until the link expires or is revoked
- short-lived links are safer than long-lived ones
- link creation and revocation are written to the audit log

That makes share links useful for narrow collaboration, but not a replacement for real user and project access.

## SSO and user access

The repo shows support for:

- Google SSO
- Microsoft SSO

Combined with per-project access, this gives teams a cleaner model than sharing one broad admin credential across every environment.

SSO is used for admin sign-in and invitation completion. Config consumers still authenticate with API keys.

For implementation details and config keys, see [Single sign-on (SSO)](/docs/operations/sso/).

## Project-level permissions

Authentication and authorization are different concerns.

A user may be able to sign in successfully but still only have access to a limited set of projects. That is the safer operating model for one Nona deployment serving multiple apps, teams, or environments.

Use project access to avoid:

- broad cross-team visibility
- accidental edits in unrelated projects
- one shared admin account becoming a bottleneck or risk

## Auditability

Security controls are stronger when identity changes and config changes are visible after the fact.

The repo includes audit-log support, which is especially relevant for:

- access changes
- config edits
- rollback actions
- share-link creation and revocation

## FAQ

### Does SSO replace API keys for runtime reads?

No.

SSO is for admin access. Runtime config consumers still authenticate with API keys.

### What should I lock down first in production?

Start with admin access, narrow API keys, limited project access, and stable JWT settings if you pin them.

### Should teams share one broad admin account?

No.

One account per operator is a safer and more auditable operating model.

### Are share links a replacement for user access?

No.

Share links are useful for narrow temporary collaboration, but they are not a replacement for normal user and project access control.

## Related docs

- [API keys](/docs/concepts/api-keys/)
- [Single sign-on (SSO)](/docs/operations/sso/)
- [Audit logs](/docs/concepts/audit-logs/)
- [Users and project access](/docs/concepts/users-and-project-access/)
- [Parameter share links](/docs/parameter-share-links/)
- [Deployment](/docs/deployment/)
