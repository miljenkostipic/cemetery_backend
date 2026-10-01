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
- Tests ship with the feature. xUnit v3, AwesomeAssertions, NSubstitute, Bogus, injected `TimeProvider`. 100% line and branch on Domain and Application. Integration tests use Testcontainers + Respawn: happy path, refusal, and cross-tenant (tenant B never sees tenant A) for every endpoint. ≥ 90% line on Api and Infrastructure. A bug fix starts with a failing test. CI must be green to merge.

Domain language, phases, and frontend rules are in [plan.md](plan.md). Current milestone is Phase 0 (walking skeleton): this solution first, CI, multitenancy, organizations and sign-in.
