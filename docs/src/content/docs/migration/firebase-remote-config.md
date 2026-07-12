---
title: Migrate from Firebase Remote Config
description: Use the Nona CLI to import Firebase Remote Config data into Nona with environment mapping, scope mapping, and dry runs.
---

Nona includes a built-in Firebase migration command.

Use it when you want to:

- leave a hosted control plane
- preserve existing parameter work
- import values into projects and environments you run yourself

## What the migration handles

- Firebase source namespaces
- scope mapping into `client`, `server`, or `all`
- condition-to-environment mapping
- content type conversion
- dry-run planning
- conflict handling

## Detailed command docs

The CLI-specific migration guide is here:

- [CLI Firebase migration reference](/cli/firebase-migration/)

Continue with:

- [Firebase concept mapping](/migration/firebase-concept-mapping/)
- [Migration validation](/migration/validation/)
