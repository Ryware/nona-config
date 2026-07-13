---
title: Audit logs
description: Understand how Nona audit logs help you track operational changes such as entry updates and share-link actions.
---

Audit logs are the operational record for sensitive admin actions.

They are especially useful for:

- incident review
- change accountability
- collaboration in teams with multiple editors

Parameter share-link creation and revocation are explicitly documented as audit-log events.

## Why audit logs matter

Audit logs help answer questions that always come up during incidents and reviews:

- who changed this?
- when did it happen?
- was the change intentional?
- what else changed around the same time?

That makes them useful for both engineering and operational workflows.

## Useful audit scenarios

- a feature flag was flipped and behavior changed unexpectedly
- a production parameter was edited during an incident
- a temporary share link was created for a teammate
- a team needs to review operational changes after a release

## Where to view them

In admin:

1. sign in
2. open `Audit Logs` from the sidebar
3. use `Filter audit trail...` to search by actor, target, or project
4. use the `Action Type` and `Environment` filters to narrow the list
5. click a row to open the details drawer
6. use `Export Logs` to download CSV or JSON

The audit log page is the main operator workflow here. The repo does not currently expose a dedicated top-level CLI command for browsing audit logs.

## What you can expect to see

The backend and admin code currently wire audit logging around actions such as:

- project creation
- user invitation and user changes
- environment creation and deletion
- parameter creation, update, deletion, and rollback
- parameter share-link creation and revocation

That means audit logs are not just a security extra. They are part of the normal runtime operations story.

## What audit logs support

Even when rollback solves the immediate issue, audit logs help with the follow-up work:

- documenting the timeline
- understanding operator actions
- improving runbooks
- reducing repeated mistakes

## Where they matter most

Audit logs are especially useful around:

- production feature flags
- incident-driven parameter changes
- temporary share-link usage
- team environments with more than one editor

## Practical review flow

When something changes unexpectedly in production:

1. open `Audit Logs`
2. filter to the relevant environment
3. search for the key, project, or teammate involved
4. open the matching row
5. compare the audit event with the parameter history on the project page
6. roll back the parameter if needed

## Related docs

- [History and rollback](/docs/concepts/history-and-rollback/)
- [Parameter share links](/docs/parameter-share-links/)
- [Kill switches](/docs/feature-flags/kill-switches/)
