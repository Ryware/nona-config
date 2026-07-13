---
title: Single sign-on (SSO)
description: Configure Google and Microsoft SSO in Nona for admin login, invitation onboarding, and project access control.
---

Nona supports Google SSO and Microsoft SSO for the admin side of the product.

That means operators can sign in with an external identity provider instead of relying only on password-based admin access.

SSO in Nona is about admin authentication and onboarding. It is not the same thing as the runtime config API authentication model, which still uses API keys.

## What SSO is used for

In the current repo, SSO is wired into:

- admin login
- invitation acceptance
- user-to-identity linking after first successful sign-in

The relevant auth endpoints are:

- `GET /auth/sso/config`
- `POST /auth/sso/google`
- `POST /auth/sso/microsoft`
- `POST /auth/invitations/{token}/sso/{provider}`

## Supported providers

The repo currently supports:

- Google SSO
- Microsoft SSO

Google is enabled when `Sso__Google__ClientId` is configured.

Microsoft is enabled when `Sso__Microsoft__ClientId` is configured.

## Configuration keys

The server reads these settings:

### Google

```text
Sso__Google__ClientId
Sso__Google__JwksUri
Sso__Google__Issuers__0
Sso__Google__Issuers__1
```

`Sso__Google__ClientId` is the key setting that enables Google sign-in.

By default, the repo uses Google's JWKS endpoint and accepted issuers. Override them only when you have a specific reason.

### Microsoft

```text
Sso__Microsoft__ClientId
Sso__Microsoft__TenantId
Sso__Microsoft__JwksUri
Sso__Microsoft__Issuers__0
Sso__Microsoft__Issuers__1
```

`Sso__Microsoft__TenantId` defaults to `common`.

That means the system can accept Microsoft identities without pinning one tenant. If you want to restrict sign-in to one tenant, set `Sso__Microsoft__TenantId` to that tenant instead of leaving it on `common`.

## What the public SSO config endpoint exposes

`GET /auth/sso/config` returns the public configuration the admin UI needs in order to show provider buttons.

From the repo, that response includes public fields such as:

- whether Google is enabled
- whether Microsoft is enabled
- Google client id
- Microsoft client id
- Microsoft authority
- Microsoft tenant id

That endpoint is designed to expose public client-side SSO wiring, not private signing secrets.

## Google behavior

Google token validation in the repo checks:

- the token signature through the configured JWKS endpoint
- the configured Google client id as the audience
- the issuer against the configured issuer list
- a non-empty email claim
- `email_verified = true`

That last point matters: an unverified Google email should not be treated as a valid admin identity.

## Microsoft behavior

Microsoft token validation in the repo checks:

- the token signature through the configured JWKS endpoint
- the configured Microsoft client id as the audience
- the issuer
- the tenant relationship
- an email-like identity claim

If you leave `Sso__Microsoft__TenantId=common`, the system allows Microsoft sign-in from different tenants and validates the issuer against the tenant in the token.

If you set a specific tenant id, the token must match that tenant.

## How users are matched

Nona does not treat any valid SSO token as enough by itself.

The current auth workflow matches the SSO identity to a Nona user account by email. On first successful sign-in, Nona links that provider identity to the user. Later sign-ins use the stored external identity link.

This is an important safety property:

- users still need a corresponding Nona account
- first login establishes the provider identity link
- later logins must match the stored provider identity

## Invitations and SSO onboarding

Invitations are also integrated with SSO.

The invitation flow in the repo supports both:

- password completion
- SSO completion

For SSO invitation completion:

- the invited email must match the SSO identity email
- a successful SSO completion consumes the invitation
- the linked user can then sign in through the provider normally

This makes it possible to invite a teammate and let them activate access with Google or Microsoft instead of forcing an initial password setup.

## What SSO does not replace

SSO does not replace:

- API keys for `/api/{environmentId}/{key}`
- project-level access rules
- audit logging

SSO answers "who can sign into the admin surface." Project access still answers "what can that user work on after sign-in."

## Recommended operating model

For a production deployment, a good SSO model is:

- enable only the providers your team actually uses
- restrict Microsoft to a specific tenant when appropriate
- invite users instead of sharing credentials
- keep project access scoped to the projects each user needs
- review audit logs after permission or identity changes

## Related docs

- [Security and authentication](/docs/operations/security-and-authentication/)
- [Users and project access](/docs/concepts/users-and-project-access/)
- [API keys](/docs/concepts/api-keys/)
- [Audit logs](/docs/concepts/audit-logs/)
