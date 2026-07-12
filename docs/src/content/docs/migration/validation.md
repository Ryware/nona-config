---
title: Migration validation
description: Validate a Firebase-to-Nona migration by checking environments, values, scopes, and application reads before cutover.
---

Do not treat a completed import as a finished migration.

Validate:

1. the target project exists
2. expected environments exist
3. important parameters were imported
4. scopes are correct for client and server reads
5. content types match expectations
6. your application can read the migrated values

## Recommended flow

- run the migration with `--dry-run`
- inspect the plan
- apply it
- test a few critical reads over HTTP or a client SDK
- verify kill switches and high-risk flags first
