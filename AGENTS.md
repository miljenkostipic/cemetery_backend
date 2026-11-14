# Cemetery backend

This repo is the ASP.NET solution. The full product plan is [plan.md](plan.md) (from Desktop `plan.rtf`, 1 Oct 2026). The Vue app is `cemetery-frontend`; it consumes the OpenAPI document this API publishes.

## Stack

PostgreSQL 17 + PostGIS (schema `cemetery`, snake_case), ASP.NET Core 10, C# 14, EF Core 10 (Npgsql + NetTopologySuite). Clean Architecture, Minimal API endpoint groups, TypedResults, built-in OpenAPI + Scalar. FluentValidation next to each use case. Hangfire on PostgreSQL. Serilog + OpenTelemetry. API version `/api/v1`.

## Layout

Dependencies point inward: `Cemetery.Api` → `Cemetery.Application` → `Cemetery.Domain`. `Cemetery.Infrastructure` implements Application ports (EF + PostGIS, storage, email, Hangfire).

```
src/Cemetery.Domain/            aggregates, value objects, events, errors — no packages
src/Cemetery.Application/       one handler per command or query, ports, validators, DTOs
src/Cemetery.Infrastructure/
src/Cemetery.Api/               endpoint groups, auth, ProblemDetails, OpenAPI
tests/Cemetery.Domain.Tests/
tests/Cemetery.Application.Tests/
tests/Cemetery.Api.IntegrationTests/   WebApplicationFactory + Testcontainers PostGIS
tests/Cemetery.Architecture.Tests/
```

## Rules

- One use case, one handler. No MediatR, no AutoMapper, no `Result<T>`. Explicit maps (or Mapperly). Typed exceptions mapped in one `IExceptionHandler` to ProblemDetails with stable error codes.
- Domain stays persistence-ignorant. EF configuration classes only. IDs are UUID v7. Domain events go out through an outbox after save.
- One decision, one place. Fail closed: unknown scope, missing setting, or empty filter returns nothing or a refusal.
- Every tenant-owned aggregate has `TenantId`. EF global query filters plus PostgreSQL row-level security; the app connects as a non-owner role. Platform admin uses explicit audited cross-tenant endpoints only.
- Auth: ASP.NET Core Identity + passkeys, httpOnly cookie for the SPA, short-lived JWT for the field PWA. Fallback policy requires authentication; public endpoints opt out. Authorisation is role per organization plus resource policies, enforced on the server and pinned by a policy snapshot test.
- Nullable reference types on. Warnings are errors. Methods ≤ 20 lines. No boolean flag parameters, no magic numbers.
- Tests ship with the feature. xUnit v3, AwesomeAssertions, NSubstitute, Bogus, injected `TimeProvider`. 91% line and 80% branch on Domain, 79% line and 69% branch on Application. Integration tests use Testcontainers + Respawn: happy path, refusal, and cross-tenant (tenant B never sees tenant A) for every endpoint. ≥ 25% line on Api and ≥ 49% line on Infrastructure. A bug fix starts with a failing test. CI must be green to merge.

## Local machine

A dev machine runs PostgreSQL with PostGIS in one of two ways. Check which one this machine has before starting anything (`docker ps`, or the Windows services list); never run both, they both want port 5432.

- **Docker.** PostgreSQL 17 + PostGIS 3.5 from `docker-compose.yml` (`docker compose up -d`), database `cemetery`, port 5432, user and password `postgres`. `appsettings.Development.json` already points at it. Do not use other Postgres containers on the machine; they have no PostGIS.
- **Installed directly.** PostgreSQL (17 or later) with the PostGIS extension, running as a Windows service on port 5432, database `cemetery`. Do not start `docker compose` on such a machine. When the owner password is not `postgres`, put `ConnectionStrings:Owner` and `ConnectionStrings:App` in .NET user secrets (`Cemetery.Api`); never put a real password in `appsettings`.

File storage (documents, grave photos, work-order photos) uses the private Cloudflare R2 bucket `R2:BucketName` once `R2:AccessKey` is set. The keys are the same as Matica's: `R2:AccountId`, `R2:AccessKey`, `R2:SecretKey`, set in user secrets locally or as `R2__AccessKey` and so on in deployment. If `R2:AccessKey` is not set, files stay in the database (`stored_files`). If it is set but another R2 value is missing, the app refuses to start. Objects are keyed `{tenantId}/{uuid v7}`. Keys written before R2 (bare UUIDs) are still read from the database.

Either way the app creates the `cemetery_app` role and applies migrations on startup (`DatabaseInitializer`).

Integration tests always start their own PostGIS container through Testcontainers, the same as CI, so they need Docker. Without Docker they skip (`DockerProbe`), and CI is where they run; do not install Docker just for them unless asked.

Domain language, phases, and frontend rules are in [plan.md](plan.md). Current milestone is Phase 0 (walking skeleton): this solution first, CI, multitenancy, organizations and sign-in.
