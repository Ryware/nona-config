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

That matters because it gives teams a cleaner way to add people than manually handing around one credential. A new user can be invited into the system and then granted access based on the projects they actually need.

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

## Related docs

- [Projects](/docs/concepts/projects/)
- [Parameter share links](/docs/parameter-share-links/)
- [Audit logs](/docs/concepts/audit-logs/)
