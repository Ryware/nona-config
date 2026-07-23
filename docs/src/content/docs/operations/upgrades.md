---
title: Upgrades
description: Upgrade a self-hosted Nona deployment carefully by preserving persistent data, keeping JWT settings stable, and validating the service after restart.
---

Because Nona is self-hosted, upgrades are an operational responsibility for your team.

The deployment guides in this repo already show the two most important principles:

- keep persistent data volumes
- keep JWT settings stable when you pin them

Everything else in an upgrade plan should support those two goals.

## A practical upgrade pattern

For most teams, the safe sequence is:

1. take a backup
2. confirm JWT settings are stable
3. replace the container image
4. start the updated service
5. validate login and one known config read

## Before upgrading

Before an upgrade:

1. know which deployment topology you are running
2. protect the persistent data volume or volumes
3. confirm your JWT settings strategy
4. plan a post-upgrade validation check

If you use the production Compose files from this repo, that also means confirming which volume names are in play:

- standalone: `nona-data`
- primary/replica: `nona-primary-data` and `nona-replica-data`

If you run the single-container Docker path instead of Compose, the same rule applies: preserve the mounted `/var/lib/nona` volume.

## Persistent data

The deployment docs explicitly say to keep the Docker volumes used by Nona when upgrading.

That matters because those volumes hold the durable application state under `/var/lib/nona`.

Do not treat container replacement as equivalent to data preservation. The container image can change while the mounted data must survive.

## First checks after restart

The first post-upgrade checks should be:

1. the container is running
2. the admin UI responds
3. a known user can authenticate
4. a known key can still be read

Do those before assuming the upgrade is complete.

## JWT stability

If you pin JWT settings, keep the same values during upgrade:

- `Jwt__Key`
- `Jwt__Issuer`
- `Jwt__Audience`

Changing them unexpectedly can make the deployment harder to reason about during a maintenance window.

This is especially important if users are already actively signing into the admin surface.

## Topology-specific upgrade thinking

### Standalone

For standalone, the main goal is to replace the running container without losing the mounted `nona-data` volume or changing pinned auth settings unexpectedly.

For the one-container Docker path, that usually means stopping the old container, starting the new image against the same volume, and validating immediately.

Nona does not automatically convert an existing sqld database to SQLite. If an existing standalone deployment is moving to SQLite, treat that as a separate storage migration: back up the volume, migrate the data deliberately, and validate the new database before retiring the old one.

### Primary/replica

For primary/replica, validate both services after the upgrade:

- the primary admin and write path
- the replica read path
- the expected port bindings
- the expected replication relationship

Because replica reads are eventually consistent, part of upgrade validation is confirming that known values are still visible where you expect them to be after the services settle.

## Good validation target

Pick one known flag or setting before the maintenance window starts.

After the upgrade, verify that exact key again. This is much more reliable than doing a vague “the UI looks fine” check.

## After upgrading

Validate:

- the service starts successfully
- the API is reachable
- admin login still works
- a known config read still works
- key operational values or flags are present

For primary/replica deployments, validate both endpoints rather than checking only one container.

## A practical post-upgrade checklist

- `docker compose ps` shows the expected services as running
- the admin UI or admin API is reachable
- a known user can authenticate
- a known config key can still be read
- in primary/replica mode, a known read succeeds from the replica path too

## FAQ

### What is the safest first step before an upgrade?

Take a backup first.

That gives you a recovery path before you touch the running deployment.

### What should stay stable during an upgrade?

The persistent data volumes and any pinned JWT settings should stay stable across the upgrade.

### How should I verify the upgrade worked?

Check that the service starts, login still works, and a known config read still succeeds.

### Is a quick UI check enough after an upgrade?

No.

A real validation read is much more reliable than assuming the upgrade worked because the UI loads.

## Related docs

- [Standalone production](/docs/deployment/standalone/)
- [Primary/replica production](/docs/deployment/primary-replica/)
- [Backups](/docs/operations/backups/)
