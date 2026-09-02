# Security pre-production checklist

Status: release-candidate validation

This checklist records security evidence for the CidadeEmDia MVP release candidate. A check is only considered complete when there is reproducible evidence from CI, homologation or the target infrastructure.

## Horizontal authorization

Automated homologation script: `infra/scripts/security-horizontal-test.sh`.

Validated evidence:

- Citizen B cannot read Citizen A occurrence by internal id.
- Citizen B cannot use Citizen A public code through the authenticated owner endpoint.
- Citizen B cannot list or add targets to Citizen A occurrence.
- Master B cannot read, accept or reject a target that belongs to Master A.
- Master A cannot decide a target that belongs to Master B.
- Unrelated Citizen and unrelated Master cannot open another target's chat.
- Subaccount permissions alone do not grant occurrence/chat access without explicit assignment.
- Citizen and Master roles cannot access the admin-only endpoint.
- Refresh token is rotated and a token revoked by logout cannot be reused.
- Test fixtures are removed after the run.

Validated marker:

```text
=== SECURITY HORIZONTAL AUTHORIZATION OK ===
```

## Release-candidate infrastructure gate

Automated gate: `infra/scripts/security-release-candidate-test.sh`.

The gate must run on the exact candidate SHA and verifies:

- public HTTPS and HTTP -> HTTPS redirect;
- baseline security response headers;
- allowed homolog CORS origin and denial of an untrusted origin;
- PostgreSQL has no published host port;
- API has no directly published host port;
- Docker backend network is internal;
- inner Nginx is exposed only on `127.0.0.1:8080`;
- `.env` is untracked and has restrictive permissions;
- runtime JWT/DB/R2/SMTP/payment secret values do not appear in tracked files, Git history or current API logs;
- R2 credentials are configured and an unsigned S3 API request is rejected with HTTP 400/401/403;
- unsigned Mercado Pago webhook is rejected with HTTP 401;
- Mercado Pago provider tables remain unchanged during the gate;
- health, web and main working tree remain clean.

Expected marker:

```text
SECURITY — RELEASE CANDIDATE: OK
```

## Dependencies

Evidence recorded in the execution board:

- `dotnet list CidadeEmDia.sln package --vulnerable --include-transitive` returned no vulnerable NuGet packages in the validated scan from 2026-08-27.
- regular CI continues to restore/build/test the final branch before merge.

Repeat the dependency scan once more immediately before the production cutover if the dependency graph changes after this release-candidate gate.

## Cloudflare R2

Validated architecture and homologation evidence:

- object reads use temporary signed URLs;
- direct browser upload uses presigned PUT;
- homologation CORS allows the required origin/methods/headers;
- invalid media signature does not become READY;
- the release-candidate gate performs an unsigned request against the R2 S3 API endpoint and requires an explicit rejection (HTTP 400/401/403), never 2xx/3xx.

This S3 API check proves that unsigned access through the application storage endpoint is rejected. Public `r2.dev`/custom-domain exposure, if ever enabled separately at Cloudflare, must remain disabled or be reviewed independently before production.

## TLS and edge

The release-candidate gate validates the homologation hostname with normal certificate verification, confirms HTTP -> HTTPS redirect and requires these response headers:

- `Strict-Transport-Security: max-age=31536000`;
- `X-Content-Type-Options: nosniff`;
- `X-Frame-Options: DENY`;
- `Referrer-Policy: strict-origin-when-cross-origin`.

The same checks must be repeated for the final production hostname during cutover.

## Database and infrastructure

Release-candidate requirements:

- PostgreSQL is not publicly reachable through Docker port publishing;
- API is not directly exposed on a host port;
- only the inner Nginx binding `127.0.0.1:8080` is published by the application compose stack;
- backend Docker network is internal;
- SSH key-only hardening remains covered by the existing KVM hardening evidence;
- backup/rollback remain part of the production cutover gate.

## Secrets and logs

Release-candidate requirements:

- `.env` is ignored by Git and remains server-local;
- `.env` permissions are `600` or `640`;
- runtime JWT, database, R2, SMTP and payment secret values are searched against tracked files, Git history and current API logs without printing the secret itself;
- no raw access/refresh token is printed by the security scripts;
- error responses must not expose connection strings or provider credentials.

## Payments and webhooks

The Mercado Pago module is implemented but provider credentials are intentionally unavailable/disabled in the current environment.

Applicable security evidence before real provider activation:

- webhook endpoint rejects an unsigned request with HTTP 401;
- signature verification uses HMAC-SHA256 and fixed-time comparison;
- provider tables remain unchanged by invalid webhook tests;
- client-side payment state is not trusted as authoritative.

The real signed webhook, provider payment lifecycle and provider E2E remain blocked on the Mercado Pago card until sandbox/production credentials are supplied. That external block does not require weakening the release-candidate security controls.

## Audio/private chat media

Chat text authorization is covered by horizontal tests. Audio was explicitly deferred from the current delivery and is therefore not part of this release-candidate security gate.

If audio is implemented later, it must receive a separate private-storage and authorization review before release.

## Release decision

The security card can be closed for the currently implemented release-candidate scope only after:

1. CI is green on the security branch;
2. the exact branch SHA passes `security-release-candidate-test.sh` on the KVM;
3. the hardening changes are merged;
4. the same smoke passes from `main`.

Production hostname/certificate/rollback checks remain part of the production Go/No-Go and must be repeated at cutover.
