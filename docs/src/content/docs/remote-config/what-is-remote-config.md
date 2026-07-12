---
title: What is remote config?
description: Understand remote config, what it solves, and how Nona delivers it as a self-hosted service.
---

Remote config is a way to store application settings outside your deployed app so you can change behavior later.

Typical examples:

- enable or disable a feature
- change text or copy
- tune thresholds or limits
- switch between old and new flows
- add a kill switch for a broken feature

In Nona, remote config is built from a few core pieces:

- a project
- one or more environments such as `development` or `production`
- config entries stored per environment
- API keys and scopes that control what can be read

## Why teams use it

Remote config helps when you need to:

- react without a redeploy
- separate environment-specific values
- roll out behavior safely
- keep client-readable and server-only values distinct

## What makes Nona different

Nona is not a hosted control plane.

It is:

- self-hosted
- Docker-first
- open source
- usable over plain HTTP

If you want to see where that matters, read [Firebase Remote Config alternative](/docs/comparisons/firebase-remote-config-alternative/).
