# Security pre-production checklist

Status: in progress

This checklist records security evidence for the CidadeEmDia MVP release candidate. A check is only considered complete when there is reproducible evidence from CI, homologation or the target infrastructure.

## Horizontal authorization

Automated homologation script: `infra/scripts/security-horizontal-test.sh`.

Required evidence:

- Citizen B cannot read Citizen A occurrence by internal id.
- Citizen B cannot use Citizen A public code through the authenticated owner endpoint.
- Citizen B cannot list or add targets to Citizen A occurrence.
- Master B cannot read, accept or reject a target that belongs to Master A.
- Master A cannot decide a target that belongs to Master B.
- Unrelated Citizen and unrelated Master cannot open another target's chat.
- Subaccount permissions alone do not grant occurrence/chat access without explicit assignment.
- Citizen and Master roles cannot access the admin-only endpoint.
- Refresh token is rotated and a token revoked by logout cannot be reused.
- Test fixtures are removed after a successful run.

Expected final marker:

```text
=== SECURITY HORIZONTAL AUTHORIZATION OK ===
```

## Dependencies

Evidence already recorded in the execution board:

- `dotnet list CidadeEmDia.sln package --vulnerable --include-transitive` returned no vulnerable NuGet packages in the validated scan from 2026-08-27.

Re-run the dependency scan against the final release candidate before production.

## Cloudflare R2

Current homologation evidence from the occurrence-media flow:

- bucket remains private;
- object reads use temporary signed URLs;
- direct browser upload uses presigned PUT;
- homologation CORS explicitly allows the homolog origin and required methods/headers;
- invalid media signature does not become READY.

Before production, repeat the configuration check for the production origin. Do not enable public bucket access as a substitute for CORS or signed URLs.

## TLS and edge

Pending final release-candidate verification:

- HTTPS valid for the production hostname;
- HTTP redirect policy confirmed;
- security headers reviewed at the edge;
- homologation-only origins removed from production policies when not required;
- origin/API exposure matches the intended architecture.

## Database and infrastructure

Pending final release-candidate verification:

- PostgreSQL is not publicly reachable;
- only required service ports are exposed;
- Docker services do not publish database ports externally;
- SSH hardening remains applied;
- backup and rollback are executable before cutover.

Use the existing KVM audit/hardening scripts as supporting evidence, not as a substitute for checking the final running state.

## Secrets and logs

Pending final release-candidate verification:

- no production secrets committed to Git;
- `.env` remains only on the server with restrictive permissions;
- JWT keys, R2 credentials, SMTP credentials and future payment credentials are not printed in application logs;
- test scripts never print raw access/refresh tokens;
- error responses do not expose secrets or database connection strings.

## Payments and webhooks

Blocked until the Mercado Pago module is implemented and explicitly enabled for this delivery.

When implemented, validate at minimum:

- webhook signature/authenticity;
- duplicate delivery idempotency;
- invalid webhook rejection;
- payment status transitions;
- no trust in client-supplied payment state.

## Audio/private chat media

The current text-chat authorization is covered by horizontal tests. Audio/private chat media requires its own storage and authorization validation when that feature is implemented.

## Release decision

This card remains in progress until all applicable items for the final release candidate have evidence. Items belonging to modules not yet implemented must remain explicitly pending rather than being marked as passed.
