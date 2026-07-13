---
title: Backups
description: Back up Nona persistent data before risky changes and treat the mounted data volumes as production state.
---

Nona stores persistent state under `/var/lib/nona` in the Docker-based deployment paths documented in this repo.

That means backups should focus on the persistent Docker volume data used by your deployment.

From the shipped app configuration, libSQL working state lives under `/var/lib/nona/`, and the default primary database path is `/var/lib/nona/primary.db`.

Even if your exact storage layout changes later, the operational rule stays the same: back up the mounted persistent state, not just the container image or Compose file.

## What to back up

At minimum, preserve:

- the persistent Docker volume behind `/var/lib/nona`
- any pinned JWT settings you manage outside the volume
- enough deployment metadata to know which topology the backup belongs to

Backing up only the container tag or deployment manifest is not enough. The durable state is in the mounted data path.

## Standalone deployment

In standalone mode, the deployment guide mounts:

- `nona-data` -> `/var/lib/nona`

Treat that volume as production data and protect it before risky maintenance or upgrade work.

For standalone, the most important backup question is simple: can you restore the contents behind `nona-data` if the host, container, or deployment changes unexpectedly?

## Simple standalone workflow

For a one-container deployment, the normal operator flow is:

1. identify the volume behind `/var/lib/nona`
2. take a host-level snapshot or backup of that volume
3. record when the backup was taken
4. perform the risky change only after the backup exists

## Primary/replica deployment

In primary/replica mode, the documented deployment creates:

- `nona-primary-data` -> `/var/lib/nona`
- `nona-replica-data` -> `/var/lib/nona`

The docs already treat both as persistent state, so backup planning should reflect that.

At minimum, be explicit about how you protect the primary's durable state. If you also preserve replica state, restores and rollouts may be easier to reason about operationally.

## Before a risky change

Before upgrades, host moves, or topology changes:

1. confirm which volumes belong to the deployment
2. confirm where JWT settings come from
3. take the backup or snapshot
4. make sure the team knows how to restore it

## What should trigger a backup

Backups are especially important before:

- upgrades
- topology changes
- storage migrations
- host replacement
- other maintenance that could affect `/var/lib/nona`

## Practical restore checklist

After a restore, validate:

1. the container starts
2. the admin UI loads
3. a known user can sign in
4. a known parameter still exists
5. a known runtime read still works

That is the real measure of whether the backup is useful.

## Good backup habits

- take a backup or snapshot before upgrades
- know which volume or storage path belongs to which service
- know which deployment a backup came from
- test restore procedures outside the middle of an incident
- keep backup timing aligned with production change windows

## Restore planning

A backup is only useful if the restore path is clear.

For Nona, that usually means knowing:

- which Docker volume or storage path must be restored
- which Compose topology the backup belongs to
- which JWT settings the restored deployment should keep using
- which host ports or service URLs the restored environment should expose

If your team pins JWT settings, restore those settings consistently with the backed-up data so the recovered environment behaves like the original deployment.

## Why this matters

Because Nona is self-hosted, data protection is part of the operating model.

Without a usable backup, an upgrade or infrastructure issue can turn into data loss instead of a recoverable maintenance event.

## Related docs

- [Standalone production](/docs/deployment/standalone/)
- [Primary/replica production](/docs/deployment/primary-replica/)
- [Upgrades](/docs/operations/upgrades/)
