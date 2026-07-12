---
title: Feature flags for backend services
description: Use Nona feature flags in backend services for route gates, operational controls, and server-only rollout decisions.
---

Backend services often need feature flags for operational control more than visual rollout.

Typical backend flag uses:

- enable or disable a route
- switch between old and new implementations
- turn off an expensive integration
- guard risky background jobs

## Why server-side flags matter

Server-side flag evaluation lets you:

- keep sensitive behavior off the client
- centralize operational decisions
- hide implementation details from frontend apps

In Nona, that is where `server` scope is especially useful.

## Recommended backend patterns

- use `server` scope for backend-only flags
- keep flag names clear and operationally meaningful
- validate the default behavior when the flag is off
- treat history and rollback as part of your incident path

## Related docs

- [Client vs server scope](/docs/concepts/client-vs-server-scope/)
- [Kill switches](/docs/feature-flags/kill-switches/)
- [.NET client](/docs/clients/dotnet/)
- [HTTP](/docs/clients/http/)
