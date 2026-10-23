# R1 Safety net

Goal: every endpoint is proven to (a) work, (b) refuse the wrong role, (c) never leak another tenant's data, before Phase 4 and the read models change the schema.

Gate: every endpoint in `EndpointDataSource` has the three tests below; the policy snapshot test is green; the RLS sweep is green.

Order: R1.1 and R1.2 first (everything else builds on them), then R1.3–R1.10 in any order, R1.11 last.

---

## R1.1 Shared `ApiFixture`

- Finding: B2
- Do:
  - One PostGIS Testcontainer per test run (`postgis/postgis:17-3.5`), registered as an xUnit v3 assembly fixture.
  - `DatabaseInitializer.ApplyAsync(owner, app)` once; app connects as `cemetery_app` (as in `WalkingSkeletonTests`).
  - Respawn (as owner) between tests; ignore `__EFMigrationsHistory` and the Hangfire schema.
  - Helpers: `SignedInClientAsync(MembershipRole role, string tenant = "A")` returning an `HttpClient` with an active organization; `SeedCemeteryAsync(client)`, `SeedGraveSiteAsync(...)`, `SeedDeceasedAsync(...)` that go through the public API, not the DbContext.
  - Move `WalkingSkeletonTests` onto the fixture.
- Don't: use EF InMemory or SQLite; skip silently when Docker is missing in CI (skip only locally).
- Done when: `WalkingSkeletonTests` runs on the fixture; the whole integration suite starts one container (check the log).

## R1.2 One access policy and `ICallerContext`

- Finding: B3
- Do:
  - In `Cemetery.Application/Abstractions`: `ICallerContext { Guid UserId; Guid OrganizationId; MembershipRole Role; }`, resolved once per request in Api (cached in `HttpContext.Items`).
  - Policies in Api: `Policies.Office` (OrganizationAdmin, CemeteryClerk), `Policies.Field` (Office + FieldWorker), `Policies.Member` (any role), `Policies.Holder` (GraveHolder). Apply with `.RequireAuthorization(Policies.X)` on each endpoint or group, replacing `"tenant"`.
  - Delete `LayoutAccess`, `RegisterAccess`, `OperationsAccess` (role part), `OfficeAccess`, and their calls. Handlers that need the organization id take it from `ICallerContext`. Keep `OperationsGate` / `BillingAccess` existence checks, renamed to `Require.CemeteryAsync` etc. in one shared static class.
  - One error code for a role refusal: `access.forbidden`. Tell the frontend (add `errors.access.forbidden` to its three locale files in a matching PR).
  - Policy snapshot test: enumerate all endpoints (method + route + policy) and compare with `tests/Cemetery.Architecture.Tests/policy-snapshot.txt`, committed.
- Don't: move authorization into the domain; leave any endpoint on only `RequireAuthorization()` unless it is in the snapshot as intentionally `Member`.
- Done when: `grep -r "RequireClerkAsync" src` is empty; the snapshot file lists every endpoint; existing Application tests pass after replacing the membership mocks with an `ICallerContext` stub.

## R1.3–R1.9 Endpoint tests per group

Groups: R1.3 Auth + Organizations + Dashboard · R1.4 Layout · R1.5 Register · R1.6 Public catalog + visibility · R1.7 Operations · R1.8 Billing · R1.9 Reports + Launch + Rights (office requests).

- Finding: B2
- Do, for **every endpoint** in the group, one test class per endpoint group file, three tests per endpoint:
  1. `Happy_path` — correct role, valid input, asserts status code and the important response fields.
  2. `Wrong_role_is_403` — for Office endpoints use a FieldWorker or GraveHolder; for Holder endpoints use a clerk. Assert `access.forbidden`.
  3. `Other_tenant_is_404` — tenant B calls with an id that belongs to tenant A. Assert 404 (never 403, never data). For list endpoints: tenant B's list does not contain tenant A's rows.
  - Public endpoints instead: unpublished register returns nothing; `hideRecentDeathsDays` hides recent deaths; hidden sites never appear.
  - Add one validation test per command endpoint (400 with the validator's error code).
- Don't: assert on display text; share state between tests; seed through the DbContext.
- Done when: the group's endpoints all have the three tests; coverage of `Cemetery.Api/Endpoints/<Group>Endpoints.cs` is 100% line.

## R1.10 RLS sweep

- Finding: B2
- Do: one test that seeds tenants A and B, then for every table in schema `cemetery` with a `tenant_id` column (read from `information_schema.columns`):
  - asserts `pg_class.relrowsecurity` and `relforcerowsecurity` are true;
  - as `cemetery_app` with no `app.tenant_id` set: `SELECT count(*)` is 0;
  - with `app.tenant_id` = A: no row has `tenant_id` = B.
- Done when: the test fails if you comment out one table's policy in `RowLevelSecurity.cs` (try it once, then revert).

## R1.11 Enums instead of string statuses

- Finding: B6
- Do: `ChargeStatus`, `PaymentStatus` and any other `const string` status/kind in Domain become enums. EF: `HasConversion<string>()` with the existing snake_case values so no data migration is needed (verify with a migration that produces no SQL). JSON: one global snake_case enum converter, so the wire format does not change.
- Done when: `openapi/cemetery.json` diff shows only enum annotations, no renamed values; all tests green.

---

## Found while working

(add items here, do not fix them in this step)
