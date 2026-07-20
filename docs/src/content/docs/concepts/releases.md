---
title: Releases
description: Learn how Nona working configuration, immutable releases, active releases, and amend flow fit together.
---

Nona separates editing from serving.

Each environment has:

- one editable **working configuration**
- zero or more immutable **releases**
- one optional **active release**

That model lets you prepare changes safely before clients receive them.

## The release model

Think of the environment in two layers:

- the **Parameters** page is the editable working configuration
- the **Releases** page is the history of immutable snapshots

Applications read from releases, not directly from the editable working configuration.

That means you can change parameters, review them, and only publish a release when you are ready.

## How clients resolve versions

Public reads work like this:

- no `version` parameter reads the environment's active release
- `version=1.2.0` reads that exact release
- `version=1.2.x` reads the highest patch in the `1.2` line

This gives you a stable exact-version path and a practical major-minor line path.

## Working configuration vs active release

Editing a parameter does **not** automatically change what clients receive.

The editable working configuration is where operators prepare the next release.

Clients only see:

- the active release when they omit `version`
- the exact or line-matched release when they request one explicitly

That separation is one of the main safety properties of the release system.

## Create a release

In admin:

1. open a project
2. switch to the target environment
3. open `Releases`
4. click **Create a version**
5. enter a major-minor version such as `1.2`
6. continue to the Parameters page
7. review or adjust the working configuration
8. click **Create release**

Nona normalizes the entered version to patch `.0`, so `1.2` becomes `1.2.0`.

Creating the release stores an immutable snapshot of the current working configuration.

## Activate a release

Creating a release does not auto-activate it.

Activation is a separate deliberate step:

1. open `Releases`
2. find the release you want clients to use by default
3. click **Activate**

After that, clients that omit `version` read that active release.

## Amend an older release line

Use **Amend** when you need a new patch from an older release line.

For example:

- `1.1.0` amended becomes `1.1.1`
- if `1.1.1` already exists, the next amend becomes `1.1.2`

In admin:

1. open `Releases`
2. click **Amend** on the source release
3. Nona loads that release into the working configuration
4. Nona automatically targets the next free patch in that line
5. review the parameters
6. click **Create release**

Amend does not ask you to type the patch version manually.

## Important amend behavior

Amending replaces the editable working configuration with the source release's parameters.

That is useful when you want to patch an older line, but it also means the current working configuration is overwritten.

If you need to preserve unrelated unpublished work, finish or capture that work first.

## Delete a release

Non-active releases can be deleted from the release list.

Deleting a release:

- removes that immutable snapshot
- does not change the editable working configuration
- requires the release to not be the active release

If the release is active, activate a different release or clear the active release first.

## Good release habits

- treat the Parameters page as the workspace for the next release
- activate releases separately from creating them
- use exact versions for strongly pinned consumers
- use `major.minor.x` line reads when clients should float to the newest patch
- amend older lines only when you intentionally want to patch that line

## Practical example

One common flow looks like this:

1. `production` has active release `1.1.0`
2. operators prepare the next working changes
3. they create version `1.2`
4. Nona composes `1.2.0`
5. they create the release
6. they activate `1.2.0` when ready

Later, if `1.1.0` needs a backport fix:

1. they click **Amend** on `1.1.0`
2. Nona targets `1.1.1`
3. they adjust the parameters
4. they create `1.1.1`

That keeps release lines explicit and understandable.

## FAQ

### Does editing parameters immediately affect clients?

No.

Editing changes the working configuration only. Clients read releases.

### Why does Create a version ask for `1.2` instead of `1.2.0`?

Because the admin flow treats the first release in a line as patch `.0` automatically.

That keeps version entry simpler while still storing full release versions.

### Does creating a release activate it automatically?

No.

Activation is a separate explicit action.

### What does Amend do?

Amend loads an existing release into the working configuration and targets the next free patch in that same line.

### Can I patch an older release line without typing the next patch version?

Yes.

That is what Amend does automatically.

## Related docs

- [Environments](/docs/concepts/environments/)
- [Parameters and content types](/docs/concepts/parameters-and-content-types/)
- [History and rollback](/docs/concepts/history-and-rollback/)
