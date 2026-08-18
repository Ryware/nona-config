---
title: AWS Parameter Store migration
description: Import AWS Systems Manager Parameter Store string values referenced by an ECS task definition into server-scoped Nona configuration.
---

Use `nona migrate parameter-store` to import Parameter Store values referenced by a local ECS task-definition JSON file.

The migrator reads each container's `secrets` mappings, uses the ECS environment-variable name as the Nona key, retrieves the referenced SSM parameter, and writes it into one explicit Nona environment. For example:

```json
{
  "name": "TRANSFER_TOKEN_SALT",
  "valueFrom": "arn:aws:ssm:eu-central-1:111122223333:parameter/app/db/transfer_token_salt"
}
```

creates the Nona key `TRANSFER_TOKEN_SALT` when the SSM parameter type is `String`.

## Requirements

- A local ECS task-definition JSON file.
- AWS credentials with `ssm:GetParameters` for every referenced parameter.
- A configured AWS region for bare parameter names. Full SSM ARNs provide their own region.
- Nona admin access through a bearer token, email/password, or a saved `nona auth login` session.

The AWS SDK default credential chain is used by default. Pass `--profile` to select a named profile from the shared AWS credentials/config files. The command never accepts access keys directly.

## Run a dry run

```bash
nona migrate parameter-store \
  --task-definition ./task-definition.json \
  --environment production \
  --profile my-aws-profile \
  --base-url https://nona.example.com \
  --project backend-service \
  --token "$NONA_ADMIN_TOKEN" \
  --dry-run
```

Dry runs retrieve and classify the referenced parameters but do not modify Nona. Output contains target keys and source references, never parameter values.

For a bare `valueFrom` such as `/app/db/host`, pass `--region` when the region is not available from the selected AWS profile or default AWS configuration:

```bash
nona migrate parameter-store \
  --task-definition ./task-definition.json \
  --environment production \
  --region eu-central-1 \
  --dry-run
```

## Apply the migration

Remove `--dry-run` after reviewing the plan:

```bash
nona migrate parameter-store \
  --task-definition ./task-definition.json \
  --environment production \
  --profile my-aws-profile \
  --base-url https://nona.example.com \
  --project backend-service \
  --token "$NONA_ADMIN_TOKEN"
```

The project and environment are created if needed. Existing Nona keys are overwritten through the normal upsert API so reruns converge to the current Parameter Store values.

## Mapping and filtering

| ECS/AWS source | Migration behavior |
|---|---|
| `containerDefinitions[].secrets[].name` | Becomes the Nona key |
| SSM parameter with type `String` | Imported as Nona `text`, scope `server` |
| SSM parameter with type `SecureString` | Skipped without requesting decryption |
| SSM parameter with type `StringList` | Skipped |
| AWS Secrets Manager ARN | Skipped with a warning |
| ECS `environment` entry | Ignored |

All containers are considered. Identical repeated mappings are deduplicated. If the same ECS environment-variable name maps to different SSM parameters, the command stops before retrieving or writing values.

The migrator retrieves all source values before making any Nona changes. A malformed reference, missing parameter, inaccessible parameter, or AWS failure stops the operation before Nona writes begin.

## Options

| Option | Meaning |
|---|---|
| `--task-definition <path>` | Required local ECS task-definition JSON file |
| `--environment <name>` | Required target Nona environment |
| `--region <region>` | Region fallback for bare parameter names |
| `--profile <profile>` | Named AWS credentials profile |
| `--dry-run` | Retrieve and display the plan without writing |
| `--base-url`, `--api-url` | Nona API base URL |
| `--project`, `--project-name` | Nona project name |
| `--token`, `--bearer-token` | Nona admin bearer token |
| `--email`, `--password` | Nona admin login credentials when no token is supplied |

Nona connection values follow the normal CLI precedence: command-line option, `NONA_CLI_*` environment variable, saved default, and matching saved login session.

## Validate the result

After applying the migration, verify that the target environment contains the expected server-scoped text keys and test real application reads before changing the ECS deployment. Continue with [Migration validation](/docs/migration/validation).
