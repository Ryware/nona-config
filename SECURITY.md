# Security Policy

## Supported Versions

Nona is under active development, with components at different maturity levels (the CLI is at v3.x; some client libraries are still pre-1.0). We support the **latest released version** of each component (server, CLI, and client libraries) with security fixes. Older versions are not patched — please upgrade to the latest release if you receive a vulnerability report.

| Component        | Supported          |
| ----------------- | ------------------ |
| Server / WebApi   | Latest release only |
| CLI                | Latest release only |
| Client libraries   | Latest release only |

## Reporting a Vulnerability

Please **do not open a public GitHub issue** for security vulnerabilities.

Instead, report it privately using one of the following methods:

- **Preferred:** Use [GitHub's private vulnerability reporting](https://github.com/Ryware/nona-config/security/advisories/new) for this repository.
- **Email:** [contact@ryware.dev](mailto:contact@ryware.dev)

When reporting, please include as much of the following as you can:

- A description of the vulnerability and its potential impact
- Steps to reproduce, or a proof-of-concept
- The affected version(s) or commit
- Any known mitigations

## What to Expect

- We will acknowledge your report as soon as possible.
- We will investigate and keep you updated on progress toward a fix.
- Once a fix is available, we will coordinate disclosure timing with you and credit reporters who wish to be credited.

## Scope

This policy covers the code in this repository, including the server, CLI, migrator, client libraries, and official Docker images. Vulnerabilities in third-party dependencies should be reported to the upstream project, but feel free to let us know as well so we can track and update accordingly.
