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

- [Standalone production](/docs/deployment/standalone)
- [Primary/replica production](/docs/deployment/primary-replica)

For most teams, standalone is the right starting point.

## Recommended first deployment

If you are deploying Nona for the first time, start with one container:

```bash
docker run -d \
  --name nona \
  --restart unless-stopped \
  -p 18080:8080 \
  -v nona-data:/var/lib/nona \
  rywaredev/nona:latest
```

Only move to the replica topology if you already know you need it.

## How to choose

Choose [Standalone production](/docs/deployment/standalone) when:

- you want the simplest production setup
- one instance is enough
- you do not need replica reads
- minimizing operational complexity matters most

Choose [Primary/replica production](/docs/deployment/primary-replica) when:

- reads are heavy enough to justify a replica topology
- eventual consistency is acceptable for read traffic
- you are comfortable operating a more complex deployment shape

## Recommended starting point

If you are unsure, start with [Standalone production](/docs/deployment/standalone).

It is simpler to operate and usually the correct first production topology unless you already know that a replica model is necessary.

## After deployment

Once the instance is live:

1. create the first admin account
2. create a project and environments
3. create one parameter and API key
4. validate a real read path
5. set up backups before relying on the instance operationally

## Related operations docs

- [Security and authentication](/docs/operations/security-and-authentication)
- [Backups](/docs/operations/backups)
- [Upgrades](/docs/operations/upgrades)

## FAQ

### What is the right first production deployment for most teams?

Standalone is usually the right first production deployment.

It is simpler to operate and usually enough unless you already know you need a replica read topology.

### When should I choose primary/replica instead of standalone?

Choose primary/replica only when read-heavy workloads justify the extra complexity and eventual consistency is acceptable for replica reads.

### Is deployment part of the product story for Nona?

Yes.

Because Nona is self-hosted, deployment is part of using the product, not a separate concern you can ignore.

### What should I do right after the deployment is live?

Create the first admin account, create a project and environments, validate a real read path, and then make sure backups are in place.
