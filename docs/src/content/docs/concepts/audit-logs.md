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

## What audit logs support

Even when rollback solves the immediate issue, audit logs help with the follow-up work:

- documenting the timeline
- understanding operator actions
- improving runbooks
- reducing repeated mistakes

## Related docs

- [History and rollback](/docs/concepts/history-and-rollback/)
- [Parameter share links](/docs/parameter-share-links/)
- [Kill switches](/docs/feature-flags/kill-switches/)
