---
title: Users and project access
description: Learn how Nona handles users, invitations, project access, and SSO-based onboarding so the right people reach the right projects.
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

## How to invite a user

In admin:

1. sign in
2. open `Users`
3. click the invite action
4. enter the user's name and email
5. choose the appropriate access or role
6. send or copy the invitation link

With the CLI:

```bash
nona users create \
  --name "Jane Doe" \
  --user-email jane@example.com \
  --role editor
```

The CLI returns the invitation result so you can deliver the invite to the teammate.

## Project access

Project access is important because Nona is designed around project boundaries.

That means access can follow the same boundary:

- one team can work on one project
- another team can work on a different project
- operators do not need access to everything by default

This is especially useful once one Nona instance serves multiple apps or services.

## How to think about access

A practical model is:

- one project per app or service boundary
- give each operator access only to the projects they actually work on
- use invitations instead of shared credentials

That keeps one Nona instance usable across multiple teams without turning it into a shared-admin free-for-all.

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

## Good operating pattern

For a real team setup:

1. create the project structure first
2. invite users individually
3. grant access by project boundary
4. review [Audit logs](/docs/concepts/audit-logs) after sensitive permission changes if needed

## FAQ

### Does SSO bypass project access control?

No.

SSO only changes how a user authenticates. Project access still determines what the user can see and edit afterward.

### Should I invite users instead of sharing one admin account?

Yes.

Invitation-based onboarding and per-user access are much safer than sharing one broad admin credential.

### Can access be limited by project?

Yes.

Project boundaries are part of the intended access-control model, especially when one Nona instance serves multiple apps or teams.

### What is the safest first collaboration model?

Create the project structure first, invite users individually, then grant each person only the project access they actually need.

## Related docs

- [Projects](/docs/concepts/projects)
- [Single sign-on (SSO)](/docs/operations/sso)
- [Parameter share links](/docs/parameter-share-links)
- [Audit logs](/docs/concepts/audit-logs)
