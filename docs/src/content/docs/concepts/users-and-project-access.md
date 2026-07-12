---
title: Users and project access
description: Learn how Nona handles users, invitations, project access, and SSO-based onboarding.
---

Nona includes user management and per-project access controls.

The repo also shows support for:

- invitations
- Google SSO
- Microsoft SSO

This gives teams a cleaner collaboration model than sharing one admin credential across every environment.

## Why this matters

Configuration systems become risky quickly when every operator shares one broad admin account.

Per-user access and project-level permissions help teams:

- reduce accidental edits
- keep ownership clear
- onboard collaborators more safely
- separate access between apps or teams

## Invitations

The repo shows invitation-based onboarding support.

That matters because it gives teams a cleaner way to add people than manually handing around one credential.

A new user can be invited into the system and then granted access based on the projects they actually need.

In the current repo, an invitation can be completed through:

- password setup
- Google SSO
- Microsoft SSO

That is useful when you want onboarding to match the identity system your team already uses.

## Project access

Project access is important because Nona is designed around project boundaries.

That means access can follow the same boundary:

- one team can work on one project
- another team can work on a different project
- operators do not need access to everything by default

This is especially useful once one Nona instance serves multiple apps or services.

## SSO support

The current repo shows support for:

- Google SSO
- Microsoft SSO

That helps teams fit Nona into existing identity workflows instead of forcing password-only administration for every user.

SSO does not bypass access control. It only changes how a user authenticates.

After sign-in, project access still determines what that user can see and edit.

## How SSO and invitations work together

The repo's SSO flow is stricter than "any valid Google or Microsoft account can log in."

The system matches the SSO identity to a Nona user account by email. During invitation completion, the SSO email must match the invited email. On the first successful SSO login, Nona links that provider identity to the user for future sign-ins.

That gives you a safer model than open self-registration with external identity alone.

## Related docs

- [Projects](/docs/concepts/projects/)
- [Single sign-on (SSO)](/docs/operations/sso/)
- [Parameter share links](/docs/parameter-share-links/)
- [Audit logs](/docs/concepts/audit-logs/)
