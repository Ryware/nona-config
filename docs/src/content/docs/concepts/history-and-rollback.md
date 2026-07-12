---
title: History and rollback
description: Use Nona config entry history and rollback to inspect changes and recover quickly from bad parameter updates.
---

Nona tracks config entry history and supports rollback to a previous version.

This matters when:

- a bad value reaches production
- you need to inspect who changed a parameter
- a temporary change needs to be reverted safely

Use the CLI or admin workflows to:

- inspect entry history
- select a previous version
- roll back the current value
