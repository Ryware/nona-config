---
title: Deployment
description: Deploy Nona in standalone or primary/replica mode and prepare production operations around storage, auth, and upgrades.
---

Nona is designed to be run by your team.

Use this section for production deployment guidance.

The two main deployment paths are:

- standalone
- primary/replica

Both are self-hosted. The right choice depends mostly on your read pattern, operational tolerance for complexity, and whether eventual consistency is acceptable.

## What deployment means in Nona

Because Nona is self-hosted, deployment is part of the product story, not an afterthought.

That means you need to think about:

- where the service runs
- how persistent data is stored
- how JWT settings are managed
- whether one instance is enough
- whether replica reads are worth the extra complexity

## Paths

- [Standalone production](/docs/deployment/standalone/)
- [Primary/replica production](/docs/deployment/primary-replica/)

For most teams, standalone is the right starting point.

## How to choose

Choose [Standalone production](/docs/deployment/standalone/) when:

- you want the simplest production setup
- one instance is enough
- you do not need replica reads
- minimizing operational complexity matters most

Choose [Primary/replica production](/docs/deployment/primary-replica/) when:

- reads are heavy enough to justify a replica topology
- eventual consistency is acceptable for read traffic
- you are comfortable operating a more complex deployment shape

## Recommended starting point

If you are unsure, start with [Standalone production](/docs/deployment/standalone/).

It is simpler to operate and usually the correct first production topology unless you already know that a replica model is necessary.
