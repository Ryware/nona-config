---
title: Operations
description: Operate a self-hosted Nona deployment with guidance on security, authentication, backups, upgrades, and day-to-day production tasks.
---

Because Nona is self-hosted, operating the service is part of the product story.

This section covers the core operational topics that sit next to deployment:

- security and authentication
- single sign-on
- backups
- upgrades

## In this section

- [Security and authentication](/docs/operations/security-and-authentication)
- [Single sign-on (SSO)](/docs/operations/sso)
- [Backups](/docs/operations/backups)
- [Upgrades](/docs/operations/upgrades)

## Typical operator tasks

Once Nona is live, the usual operational work is:

1. secure admin access
2. manage users and project access
3. back up `/var/lib/nona`
4. review audit history after sensitive changes
5. upgrade the container without losing persistent data

That is the sequence this section is designed to support.

## When to read this section

Use these docs when:

- you are preparing a production deployment
- multiple teams or users will operate the service
- you are planning maintenance work
- you want a safer self-hosted operating model

## Start here

If you are setting up a real production instance, begin with:

1. [Security and authentication](/docs/operations/security-and-authentication)
2. [Backups](/docs/operations/backups)
3. [Upgrades](/docs/operations/upgrades)

## Related docs

- [Deployment](/docs/deployment)
- [Users and project access](/docs/concepts/users-and-project-access)
- [Audit logs](/docs/concepts/audit-logs)

## FAQ

### When should I start reading the operations docs?

Start when you are preparing a real production deployment or when more than one person will operate the instance.

### Are operations docs only about infrastructure?

No.

They also cover operator workflows such as admin access, backups, upgrades, and reviewing operational history.

### What should I secure first in a production setup?

Start with admin access and authentication, then make sure backups are in place before treating the instance as production-ready.

### Why are operations part of the product story for Nona?

Because Nona is self-hosted.

That means operating the service is part of using the product correctly, not a separate concern you can ignore.
