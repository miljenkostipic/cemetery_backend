# Findings (backend)

Review of 22 Oct 2026 on `fix-payments-migration-order` (3 commits ahead of `main`). The tests were not run during the review.

Keep: Clean Architecture layering, one handler per use case, the domain rule classes, Minimal API endpoint groups, ProblemDetails with stable error codes, EF query filters + PostgreSQL RLS, Testcontainers.

| # | Finding | Evidence | Refactoring |
| --- | --- | --- | --- |
| B1 | Phase 4 (rights and holders) is missing | No `GraveRight`, `Reservation`, `Holder` or `SiteDocument` in `Cemetery.Domain`. Holders are free-text `HolderName` on `Charge` and `MonumentPermit`. The frontend calls 12 endpoints that do not exist: `/grave-rights/{id}/renewals|transfers|inheritances`, `/grave-sites/{id}/right|reservations|documents`, `/reservations/{id}/cancellations`, `/me/grave-sites`, `/me/charges`, `/me/charges/{id}/payments`, `/me/payments/{id}`, `/me/documents`. | New `Rights` module (R2.1–R2.6). |
| B2 | Integration tests barely exist | `tests/Cemetery.Api.IntegrationTests` has one test (`WalkingSkeletonTests`) for about 70 endpoints. AGENTS.md requires happy path, refusal and cross-tenant tests per endpoint. | Shared fixture + tests per endpoint group (R1.1, R1.3–R1.10). |
| B3 | Authorization is copied 5 times | `LayoutAccess`, `RegisterAccess`, `OperationsAccess`, `OfficeAccess`, `BillingAccess`. Billing, Launch and Operations throw `register.forbidden`. `OfficeAccess.RequireClerkAsync` reads the role twice. Every handler injects `ICurrentUser`, `ITenantContext` and `IMembershipRepository` only to check the role. | One policy set + `ICallerContext` (R1.2). |
| B4 | No paging; occupancy computed on every read | No `Skip`/`Take` in `src`. `ListGraveSitesHandler` loads all sites and all interments of a cemetery and computes status in memory per request. | Persisted occupancy (R2.7), keyset paging (R2.8). |
| B5 | Promised infrastructure missing | No outbox or domain events. `WebhookSubscription` rows are stored but never delivered. No Monri / PayPal / Payrexx client (settings only). `MaticaLink` rows never reach Matica. | R5. |
| B6 | Stringly typed statuses | Billing uses `const string` statuses (`ChargeStatuses`); Layout and Operations use enums. | R1.11. |
| B7 | Handlers registered by hand | `Cemetery.Application/DependencyInjection.cs` is 127 lines, one per handler. A missing line fails only at runtime. | R0.4. |
| B8 | OpenAPI contract drift | `openapi/cemetery.json` last changed 19 Oct; billing endpoints (21–22 Oct) are missing. The frontend keeps its own different copy. | R0.2. |
| B9 | Coverage not enforced | CI runs `dotnet test` only. | R0.3. |
| B10 | Branch hygiene | Payments migration fix not merged; local `AGENTS.md` edit uncommitted. | R0.1. |

Smaller: split files over about 200 lines when you touch them (`Layout/GraveSites.cs`, `Register/DeceasedHandlers.cs`, `Public/VisibilityCommands.cs`, `Persistence/PublicCatalogSql.cs`).

## API additions for design C (frontend redesign)

| Endpoint | Serves | Notes |
| --- | --- | --- |
| `GET /cemeteries/{id}/map` | Full-screen map + layers panel | GeoJSON FeatureCollection: sections, sites (`id`, `code`, `kind`, `status`, `used`, `capacity`, `hidden`), entrances, paths; plus `counts` per status. ETag. Built in SQL with `ST_AsGeoJSON`. |
| `GET /tiles/{cemeteryId}/{z}/{x}/{y}.mvt` | Very large cemeteries | Deferred. Only when one cemetery passes about 5,000 sites. |
| `GET /grave-sites/{id}/dossier` | Floating grave panel | Site, occupancy slots, interments, current right + holders, this year's fee status, photo URL, open work orders. |
| `GET /search?q=&cemeteryId=` | Top search bar | Typed hits (`deceased`, `site`, `holder`) with `siteId` + centroid. `unaccent` + `pg_trgm`. |
| `GET /cemeteries/{id}/deceased?...&cursor=` | Register list over the map | Paged, filtered, each row with `siteId` + centroid. |
| `GET /public/memorials/{id}` (extended) | Public wayfinding | Adds centroid, section, nearest entrance, others buried in the same grave. |

`split`, `merge`, `close` and `reopen` should return the changed map features.
