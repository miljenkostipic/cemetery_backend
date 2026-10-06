# Cemetery — Master Plan

1 Oct 2026 · @Miljenko Stipic

Source: `plan.rtf` on the desktop.

## Vision and scope

Cemetery gives any cemetery operator — a parish, a municipal utility company, or a private owner — one system to map its grounds, keep the burial register, manage grave-site rights, and let the public find a grave. The first public release (end of Phase 3) is a mapped cemetery with a searchable register and a public "find a grave" experience.

### Two layers, one product

- **Administrative layer** — operators draw the cemetery, register grave sites, record interments, assign holders, manage fees, and schedule work.
- **User layer** — the public browses the cemetery, searches the deceased, opens a memorial page, and is guided to the grave on a map. Logged-in grave holders also see their own grave sites, documents, and fees.

### Roles

| Role | Layer | What they do |
| --- | --- | --- |
| Platform admin | Admin | Provisions organizations, monitors health |
| Organization admin | Admin | Owns settings, users, cemeteries of one operator |
| Cemetery clerk | Admin | Keeps the register: grave sites, interments, holders, documents |
| Field worker | Admin (mobile) | Sees today's work orders, confirms burials and works on site |
| Grave holder | User (signed in) | Sees own grave sites, fees, documents; requests services |
| Visitor | User (anonymous) | Searches, browses the map, reads memorial pages |

### Goals

- Every grave site is on the map and every interment is in the register, with a full audit trail.
- A visitor finds a named person's grave in under a minute, on a phone, standing at the gate.
- Clerks replace paper registers and spreadsheets without losing history (import from day one of onboarding).

### Non-goals for v1

- Funeral-home business management (coffins, catering, invoicing for funeral services).
- Monument design or 3D visualisation.
- Native mobile apps — the user layer and the field tools ship as a PWA.

## Tech stack and engineering principles

The stack is fixed: PostgreSQL, ASP.NET Core 10 with C#, and Vue 3 with PrimeVue in strict TypeScript. Everything below is a rule, not a suggestion — a pull request that breaks one is not merged.

| Layer | Choice | Notes |
| --- | --- | --- |
| Database | PostgreSQL 17 + PostGIS | Schema `cemetery`, snake_case, `pg_trgm` + `unaccent` for diacritic-insensitive name search |
| Backend | ASP.NET Core 10, C# 14, EF Core 10 (Npgsql + NetTopologySuite) | Clean Architecture, Minimal API endpoint groups, TypedResults, built-in OpenAPI |
| Validation | FluentValidation | Validators live next to their use case |
| Background jobs | Hangfire on PostgreSQL | Same as Matica |
| Logging and tracing | Serilog + OpenTelemetry | Structured logs, traces per request |
| Frontend | Vue 3.5, Vite 7, TypeScript strict, PrimeVue 4 (styled, custom preset), Tailwind 4 + tailwindcss-primeui | `<script setup lang="ts">` only, `allowJs: false` |
| Frontend state | Pinia 3 (client state) + TanStack Vue Query (server state) | No server data copied into Pinia |
| Forms | `@primevue/forms` + Zod schemas | One schema per form, typed end to end |
| API contract | OpenAPI → openapi-typescript + openapi-fetch | One generated, typed client; no hand-written DTO interfaces |
| Maps | MapLibre GL JS + Terra Draw | Open source, vector tiles served from PostGIS |
| i18n | vue-i18n (hr, en, de) + translation entities in the backend | Croatian first, multilingual by design |

### Clean Code and SOLID

- **Single responsibility.** One use case = one handler class. A service that grows a second job is split into a named policy, resolver, or calculator.
- **Open/closed.** New grave-site types, fee rules, or import formats arrive as new strategy classes, never as another switch branch.
- **Liskov and interface segregation.** Small ports (`IGraveSiteRepository`, `IClock`, `IFileStorage`), no "god" repositories; no interface with methods a caller does not need.
- **Dependency inversion.** Domain and Application depend on nothing outside themselves; Infrastructure implements their ports. Enforced by architecture tests.
- **One decision, one place.** Who may edit, whether a site is free, what is public — each rule lives in exactly one class and is called everywhere.
- **Fail closed.** Unknown scope, missing setting, or empty filter yields nothing or a refusal, never the whole dataset.
- **Names over comments.** Ubiquitous language from the domain section in code, API, and UI. Methods ≤ 20 lines, no boolean flag parameters, no magic numbers.
- **No `any`, no nulls leaking.** Nullable reference types and TS strict + `noUncheckedIndexedAccess` on; warnings are errors.

Tests are part of the feature. No production code merges without tests that cover it; coverage gates in CI block the merge (details in [Testing strategy](#testing-strategy)).

**UI/UX standard.** WCAG 2.2 AA, mobile-first (verified at 390 px and desktop), every list has empty, loading, and error states, every destructive action is confirmed or undoable, every map action has a list/keyboard alternative, and no hard-coded strings.

## What we take from Matica — and what we upgrade

Matica's rules from the stabilisation plan carry over as-is; its structure gets upgraded where Matica itself has already recorded the pain (anaemic models, two API layers, no linter, `any` sprawl, InMemory tests). Reviewed in `maticabackend` and `maticafrontend` on 1 Oct 2026.

### Kept as-is

- PostgreSQL with snake_case naming (EFCore.NamingConventions), its own schema, migrations for every change.
- FluentValidation, Serilog, Hangfire on Postgres, API versioning (`/api/v1`).
- Typed exceptions mapped in exactly one place — no `Result<T>`, no controller-local catch.
- "One decision, one place", "fail closed", "authorisation on the server" from `STABILISATION_PLAN.md` §0.1.
- Policy matrix test + endpoint-policy snapshot test: every endpoint's policy is pinned by a test.
- `TimeProvider` injected everywhere (Matica's `FixedTimeProvider` fixture), Bogus for test data, 100% line/branch threshold on tested layers.
- Gold Standard UI documents (Forms, DataTable, Dialogs, View pages, Dashboard) written before the first page, with one reference implementation each.
- `ResponsiveDataView` and table-state preservation — but on every table from day one, not 3 of 13.
- i18n hr/en/de with translation entities in the backend and a `check-i18n` script in CI.
- Security lessons from `SECURITY_ISSUES.md` built in from the start: no seeded default credentials, refresh-token rotation with reuse detection, `jti` + revocation, rate-limited login, fallback "authenticated" policy, no user enumeration, magic-byte MIME checks, no inline SVG, CSV-injection-safe exports, no SSRF from URL fetches.

### Upgraded

| Matica today | Cemetery | Why |
| --- | --- | --- |
| One web project, Controller → Service → Repository, models without behaviour | Clean Architecture: Domain / Application / Infrastructure / Api, rich aggregates | Business rules testable without a database; SOLID enforced by project references |
| AutoMapper | Explicit mapping methods (or Mapperly source generator) | AutoMapper is now commercially licensed; explicit maps are compile-time safe |
| FluentAssertions 8 | AwesomeAssertions (or Shouldly) | FluentAssertions 8 moved to a paid licence |
| EF Core InMemory in tests | Testcontainers PostgreSQL + PostGIS, Respawn between tests | InMemory hides real SQL, constraints, and spatial queries |
| Swashbuckle | Built-in Microsoft.AspNetCore.OpenApi + Scalar UI | Native in .NET 10, feeds the TS client generator |
| Exception middleware | `IExceptionHandler` → RFC 9457 ProblemDetails | Same idea, framework-native |
| Data annotations on entities | EF configuration classes only; domain stays persistence-ignorant | Clean domain |
| JWT + Identity + Fido2 package | Identity with .NET 10 built-in passkeys, httpOnly cookie for the SPA | Fewer packages, phishing-resistant sign-in |
| No ESLint/Prettier | ESLint flat config + Prettier + vue-tsc in CI from commit one | Avoids the 803-file formatting diff Matica now faces |
| `useApi.ts` and `services/api.ts` side by side | One generated, typed client from OpenAPI | Matica's API-layer split, solved at birth |
| PrimeVue 3, Vite 5, Tailwind 3 | PrimeVue 4 presets, Vite 7, Tailwind 4 | Start on current majors; design tokens instead of CSS overrides |
| ~1,460 `: any` lines, `allowJs` on | `no-explicit-any` = error, `allowJs: false` | Typed from day one |
| Router split after it reached 4,258 lines | Routes per feature module from the start | |
| Pinia stores holding server data | TanStack Vue Query for server state, Pinia for UI state only | Caching, retries, and invalidation for free |
| Components grouped by type (`components/`, `pages/`) | Feature folders (`features/graves/…`) with `shared/ui` | Change one feature in one place |
| ~300 frontend tests for ~990 components | Coverage gate in CI, Playwright + axe on every critical journey | "Everything is tested" made enforceable |

Matica already models Cemetery (name, address, GPS point, image) and DeathCertificate (deceased, date of death, funeral, cemetery). Cemetery should own the richer model and expose an integration API that Matica can call later (Phase 7).

## Domain model

The heart of the domain is the grave site: a place on the map with a capacity, holders who hold the right to use it, and a history of interments. Everything else — the map, fees, work orders, the public memorial — hangs off it.

### Bounded contexts

| Context | Owns | Key aggregates |
| --- | --- | --- |
| Layout | The physical cemetery and its map | Cemetery, Section, GraveSite (geometry, type, capacity, status) |
| Register | Who lies where, and when | Deceased, Interment, Exhumation / Transfer |
| Rights | Who may use a grave site | GraveRight (holders, term, transfers), Reservation |
| Billing | What holders owe and paid | FeeSchedule, Charge, Payment |
| Operations | Work on the ground | Funeral (scheduled interment), WorkOrder, MonumentPermit |
| Memorial | What the public sees | MemorialPage (photo, epitaph, visibility) — a read model over Register |
| Identity and Organization | Tenancy and access | Organization, User, Membership (role per organization) |
| Documents and Audit | Evidence and history | Document (scans, contracts, permits), AuditEntry |

### Ubiquitous language

Croatian term in brackets — used in UI copy.

- Cemetery (groblje) → Section (polje / parcela) → optional Row (red) → Grave site (grobno mjesto).
- **Grave-site type:** single grave, double/family grave, tomb (grobnica), urn niche (kolumbarij), ossuary, memorial-only. Each type defines its default capacity (burial positions / levels) and allowed interment kinds.
- **Burial position** (ukopno mjesto) — one slot inside a grave site; a tomb may have several levels.
- **Interment** (ukop) — placing a deceased (coffin or urn) into a position on a date; exhumation and transfer move remains out or between sites.
- **Rest period** (turnus ukopa) — minimum years before a position may be reused; configurable per cemetery.
- **Grave right** (pravo korištenja) — a time-bound or perpetual right held by one or more holders (korisnici), transferable by inheritance or contract.
- **Annual fee** (grobna naknada) — the recurring charge per grave site, from a fee schedule.
- **Register** (grobni očevidnik) — the legal record of grave sites, holders, and interments.

### Grave-site lifecycle

Planned → Available → Reserved → Occupied (n of capacity) → Full → (rest period ends) → Reusable, plus Closed (protected, historic, or not usable). Status is derived from interments, rights, and rest periods, never typed by hand.

### Invariants enforced in the domain

1. An interment needs a free position that allows its kind (coffin vs urn) and, where required, the holder's consent.
2. A position cannot be reused before its rest period ends, unless the remains were exhumed or transferred.
3. A grave site has at most one active grave right; holders' shares are recorded on it.
4. Register entries are never deleted — corrections create a new version with a reason (audit).
5. Living holders' personal data is never public; the deceased's public visibility follows the organization's policy and the holder's opt-out.
6. Geometry of grave sites inside a section must not overlap (PostGIS check at the boundary).

## Architecture

Two repositories: `cemetery-backend` (ASP.NET solution, migrations, backend CI) and `cemetery-frontend` (Vue app, frontend CI). The frontend consumes the backend's published OpenAPI document to generate its client. The empty `miljenkostipic/cemetery` repo can be renamed to one of them or archived. Dependencies point inward only: Api → Application → Domain, with Infrastructure plugged in behind Application's ports. The system runs as one shared, multi-tenant service (see [Multitenancy](#multitenancy)).

### Backend solution

```
src/
  Cemetery.Domain/          aggregates, value objects, domain events, domain errors — no package references
  Cemetery.Application/     use cases (one handler per command/query), ports, validators, DTOs
  Cemetery.Infrastructure/  EF Core + PostGIS, repositories, file storage (S3/R2), email, Hangfire jobs
  Cemetery.Api/             Minimal API endpoint groups per feature, auth, ProblemDetails, OpenAPI
tests/
  Cemetery.Domain.Tests/
  Cemetery.Application.Tests/
  Cemetery.Api.IntegrationTests/   WebApplicationFactory + Testcontainers (PostGIS)
  Cemetery.Architecture.Tests/     layer rules, naming, no forbidden references
```

- Use cases without a mediator library. Small `ICommandHandler<TCommand, TResult>` / `IQueryHandler<TQuery, TResult>` interfaces, registered by assembly scan; cross-cutting concerns (validation, transaction, logging, audit) as decorators. No MediatR licence, no magic.
- Reads vs writes. Commands go through aggregates and repositories; queries may read projections directly with EF `AsNoTracking` or SQL for maps and search.
- IDs are UUID v7 (`Guid.CreateVersion7()`), sortable and index-friendly.
- Domain events (e.g. `IntermentRecorded`) are dispatched after save to update read models, the audit log, and notifications — through an outbox table so nothing is lost.
- Errors: domain/application throw typed exceptions; one `IExceptionHandler` maps them to ProblemDetails with stable error codes the frontend translates.
- Auth: ASP.NET Core Identity + passkeys, cookie session for the SPA, short-lived JWT for the field PWA; a fallback policy requires authentication, public endpoints opt out explicitly. Authorisation = role per organization + resource policies ("clerk of this cemetery").
- Tenancy: every aggregate carries `TenantId`; a global query filter and a policy test make cross-organization reads impossible.

### Multitenancy

Shared database, shared schema, from day one.

- Tenant = organization (a cemetery operator). Every tenant-owned table has a non-null `tenant_id`; every unique index and foreign key includes it.
- Resolution: admin requests take the tenant from the signed-in membership (users may belong to several organizations and switch between them); public requests take it from the subdomain or a custom domain (`{slug}.<product domain>`).
- Enforcement in two layers: an `ITenantContext` drives EF Core global query filters and stamps `tenant_id` on insert; PostgreSQL row-level security policies on `current_setting('app.tenant_id')` back it up, so a forgotten filter or raw SQL still cannot cross tenants. The app connects as a non-owner role so RLS cannot be bypassed.
- Everything else is tenant-scoped too: Hangfire jobs carry the tenant id, file-storage keys are prefixed by tenant, caches are keyed by tenant, rate limits apply per tenant.
- Per-tenant settings: rest periods, grave-site types, fee schedules, public-visibility policy, languages, branding, and payment-provider credentials (encrypted at rest).
- Platform admin works through explicit, audited cross-tenant endpoints only — never by switching off filters.
- Lifecycle: tenant onboarding wizard, full tenant export, and tenant offboarding (hard delete after a retention period) are product features, not scripts.
- Shared-service bonus: where operators opt in, the public can search the deceased across all cemeteries on the platform.

### Mapping and GIS

- Geometry stored as PostGIS `geometry(Polygon, 4326)` for sections and grave sites, `Point` for markers; GiST indexes.
- Map tiles generated on request with `ST_AsMVT` from a tile endpoint and cached — smooth on a phone with tens of thousands of sites.
- Admin drawing with Terra Draw on MapLibre: draw a section, then generate a grid of grave sites (rows × columns, size, spacing, rotation) instead of drawing each by hand.
- Base layers: OpenStreetMap vector tiles, plus an uploaded georeferenced plan or drone orthophoto as an overlay.
- Cemeteries without any geodata use a schematic mode: sections and rows as a grid, upgraded to real geometry later without losing data.

### Frontend structure

```
src/
  app/            bootstrap, router assembly, PrimeVue preset, i18n setup
  features/
    cemeteries/   pages, components, composables, api (Vue Query hooks), routes.ts, __tests__
    grave-sites/
    register/
    rights/
    public-search/
  shared/
    api/          generated OpenAPI types + one typed client
    ui/           design-system wrappers (AppDataTable, AppForm, EmptyState, ConfirmAction…)
    composables/  useDate, useTableState, usePermissions
```

- Two route trees with separate layouts: `/admin/**` (dense, keyboard-first) and the public site `/` (calm, map-first, large touch targets).
- PrimeVue 4 in styled mode with one custom preset (design tokens for colour, radius, spacing, dark mode); no CSS overrides of PrimeVue internals.
- The public layer and field tools are an installable PWA; field work orders work offline and sync when back online (Phase 6).

## Testing strategy

A feature is done only when its tests are merged with it and CI's coverage gates pass. Tests are written first for domain rules (TDD), alongside the code everywhere else.

| Level | Backend | Frontend | Gate |
| --- | --- | --- | --- |
| Unit | xUnit v3 + AwesomeAssertions + NSubstitute; Domain and Application, no database | Vitest + Vue Testing Library + MSW; components, composables, stores, Zod schemas | 100% line and branch on Domain, Application, composables, stores, utils |
| Integration | WebApplicationFactory + Testcontainers (PostgreSQL + PostGIS) + Respawn; every endpoint, real SQL, real spatial queries | Feature pages mounted with router, i18n, Vue Query and MSW | ≥ 90% line on Api, Infrastructure and pages; every endpoint has a happy-path, a refusal, and a cross-tenant test (tenant B never sees tenant A) |
| Architecture and contract | Layer rules (ArchUnitNET), policy matrix + endpoint-policy snapshot (from Matica), OpenAPI snapshot (Verify) | Generated client compiles; vue-tsc with zero errors | Any diff fails the build until reviewed |
| End to end | — | Playwright on critical journeys, at 390 px and 1440 px, with axe-core accessibility checks | Zero serious/critical axe violations |
| Mutation (nightly) | Stryker.NET on Domain | StrykerJS on composables | Mutation score ≥ 80%, trend tracked |

### Rules

- Tests assert behaviour, never display text or English strings (Matica lesson); use error codes and `data-testid` / roles.
- Time, IDs, and randomness are injected (`TimeProvider`, ID generator) so tests are deterministic.
- Test data from builders (`GraveSiteBuilder.Tomb().WithCapacity(4)`) on top of Bogus — readable, reusable.
- A bug fix starts with a failing test that reproduces it.
- CI pipeline per pull request: restore → build (warnings as errors) → lint/format → unit → integration → coverage gates → E2E on a preview environment. Nothing merges red.

Critical E2E journeys (grow each phase): sign in with passkey; draw a section and generate grave sites; record an interment; visitor searches a name and is guided to the grave; holder pays an annual fee; field worker completes a work order offline.

## Phases and milestones

Eight phases, each ending in a milestone that is shippable and demoable; the first public release is M3. Every phase inherits the Definition of Done below, so no phase ends with testing or UX debt.

Phases run in order; each milestone is a gate the next phase starts from. Phases 0–3 make the first public release.

Progress checked 5 Oct 2026. Phase 0 is in use: sign-in, an organization, and an empty dashboard. Phase 1 is in use on a local cemetery: sections, a grave-site grid, split, merge, close, and reset. The milestone exits below stay open until the Definition of Done is met. There is no audit log yet, CI does not enforce coverage, and there is no Playwright run.

### Definition of Done (every feature, every phase)

- Domain rules covered by unit tests; endpoints covered by integration tests; coverage gates green
- Authorisation server-enforced and pinned in the policy snapshot; fails closed
- UI follows its Gold Standard page; verified at 390 px and desktop; empty, loading, and error states; axe clean
- hr, en, de strings complete; no hard-coded text
- Audit entries written for every register change
- OpenAPI snapshot and generated client updated

### Phase 0 — Foundations → M0 "Walking skeleton"

- [x] Two repos (`cemetery-backend` first, then `cemetery-frontend`), solution and frontend scaffold as in Architecture; ESLint, Prettier, `.editorconfig`, analyzers with warnings as errors.
- [x] CI builds and tests both repos. The PostGIS integration test exists and skips when Docker is absent.
- [ ] Coverage gates, Playwright, and a preview environment per pull request.
- [x] Multitenancy foundation: tenant resolution, `tenant_id` + EF query filters + PostgreSQL row-level security, an architecture test that every tenant-owned entity is filtered and has an RLS policy, and a cross-tenant integration test.
- [x] Organizations, users, memberships and roles, sign-in (passkey + password), and an organization switcher.
- [x] Invitations on the API.
- [ ] Invitation screens in the app.
- [x] PrimeVue preset, Gold Standard notes, and an empty state.
- [ ] Gold Standard reference pages and the shared table wrappers (`ResponsiveDataView`, table-state preservation).
- **Exit:** a user signs in, creates an organization, sees an empty dashboard, and provably cannot reach another organization's data. Sign-in, the organization, and the dashboard work. The cross-tenant proof runs in CI when Docker is present. E2E and coverage gates are still open.

### Phase 1 — Cemetery layout and map → M1 "Mapped cemetery"

- [x] Cemeteries, sections, rows, grave-site types and capacities.
- [x] Map editor: draw a section, generate a grave-site grid (rows, columns, size, spacing, rotation), split, merge, and close. Overlay a georeferenced plan. Schematic mode for a cemetery without geodata.
- [x] Remove a section that has no closed grave. Undo an accidental split or close.
- [x] Grave-site list and detail with derived status; selecting a site on the map or in the list shows the same site.
- [ ] Reshape a section or grave site after it has been saved. The API accepts a new outline; the screen does not send one yet.
- **Exit:** a clerk maps a real cemetery (pilot) and every grave site has an ID, type, and geometry. The layout screen is in use on a local cemetery, and each saved site has an id, type, and geometry. The pilot is not signed off, and these endpoints are not all covered by integration tests.

### Phase 2 — Register → M2 "Digital register"

- Deceased records, interments (coffin/urn, position, date), exhumations and transfers, rest-period rules.
- Assigning a burial location: a picker that offers only valid free positions, on map and in list.
- Excel/CSV import of legacy registers with a dry-run report; audit log and correction workflow; printable register extract.
- **Exit:** the pilot's existing register is imported and every interment is placed on the map.

### Phase 3 — Public layer → M3 "Find a grave" (first public release)

- Public cemetery map, diacritic-insensitive search by name and years (one cemetery, or all opted-in cemeteries on the platform), memorial page (photo, life dates, epitaph).
- "Take me there" with the phone's location; QR codes for grave markers linking to the memorial page.
- Visibility policy per organization and opt-out per grave; SEO and share previews for memorial pages.
- **Exit:** a visitor at the gate finds a named grave in under a minute on a phone (usability test with 5 people).

### Phase 4 — Rights and holders → M4 "Holders portal"

- Grave rights: holders and shares, terms, renewals, transfers and inheritance, reservations.
- Documents attached to sites and rights (contracts, permits, scans) with safe upload.
- Holder accounts: "my grave sites", documents, requests to the office; holder consent step in the interment flow.
- **Exit:** holders of the pilot cemetery log in and see their grave sites; clerks no longer keep holders on paper.

### Phase 5 — Fees and payments → M5 "Billing"

- Fee schedules (annual fee, one-off services), yearly charge run as a background job, reminders.
- Online payments with the same providers as Matica: Monri (cards; HR and BiH), PayPal, and TWINT via Payrexx (Switzerland). Each operator connects its own merchant account, so money goes straight to the operator. One `IPaymentGateway` per provider behind a provider-agnostic payment service, idempotent webhook handlers, and the payment state machine from Matica's `TWINT_PAYMENTS_PLAN.md`. Manual (cash/bank) payments, receipts, arrears report.
- **Exit:** a full yearly billing cycle runs end to end on staging with real pilot data.

### Phase 6 — Operations → M6 "Field ready"

- Funeral calendar (scheduling interments, chapel/mortuary booking, conflicts).
- Work orders (digging, monument installation, maintenance) with monument permits.
- Field PWA: today's tasks, map navigation, photos, offline sync.
- **Exit:** a field worker completes a burial work order on site, offline, and it syncs.

### Phase 7 — Integrations, reporting and launch → M7 "General availability"

- Reports and dashboards (occupancy, free capacity, upcoming rest-period ends, revenue).
- Integration API and webhooks; Matica integration (link DeathCertificate to an interment).
- Hardening: security review against Matica's `SECURITY_ISSUES.md` list, load test of map tiles and search, backups and restore drill, GDPR records.
- Onboarding kit: import templates, user docs and video guides per role.
- **Exit:** second and third organizations onboarded without developer help.

## Decisions, open questions and risks

### Decisions

| # | Decision | Status |
| --- | --- | --- |
| D0 | PostgreSQL, ASP.NET Core 10 / C#, Vue 3 + PrimeVue, TypeScript only | Decided |
| D1 | One shared multi-tenant service; shared database and schema, `tenant_id` + EF filters + PostgreSQL row-level security, from day one | Decided 1 Oct 2026 |
| D2 | Clean Architecture with rich domain, no mediator library | Proposed |
| D3 | No AutoMapper, MediatR or FluentAssertions (licensing); explicit mapping, own handlers, AwesomeAssertions | Proposed |
| D4 | PostGIS + MapLibre + Terra Draw, tiles via `ST_AsMVT` | Proposed |
| D5 | Payments as in Matica: Monri, PayPal, TWINT via Payrexx; each operator uses its own merchant account | Decided 1 Oct 2026 |
| D6 | Two repositories: `cemetery-backend` (started first) and `cemetery-frontend` | Decided 1 Oct 2026 |

**Isolation check behind D1.** A shared service is appropriate here; Matica's one-instance-per-parish reason does not carry over.

- **Different data.** Matica holds sacramental records (religious data, a GDPR special category), minors, and confidential pastoral data. Cemetery holds grave sites, the deceased, and holders' contact and payment data — ordinary personal data.
- **The deceased are outside GDPR.** Recital 27 leaves deceased persons' data to member states, and Croatia has no specific rules on it.
- **The register is meant to be public.** Croatia's new cemetery law (NN 78/25, 80/25, in force since May 2025) requires registers of the deceased and grave records that are publicly accessible and partly online.
- **What still needs care:** living holders' data (names, addresses, OIB, payments) stays private and tenant-isolated; do not store cause of death (Matica's DeathCertificate has it — Cemetery doesn't need it); sign a data-processing agreement with each operator, host in the EU, and keep a per-tenant export and delete. Bosnia and Herzegovina's and Switzerland's rules are still to be checked before onboarding operators there.

### Open questions

- Who is the pilot operator, and what do they have today — paper register, Excel, CAD plan, drone imagery?
- Which countries first (Croatia, Bosnia and Herzegovina, Switzerland)? Croatia's new cemetery law leaves register details, rest periods, right terms, and fees to each municipality's cemetery decision, so these must be configurable per tenant.
- Public visibility default for recent deaths (e.g. hide for N days) and who may opt out.
- Product name — "Cemetery" is the working title.
- Standalone product, or sold alongside Matica to parishes that run a cemetery?

### Risks

| Risk | Mitigation |
| --- | --- |
| Data leaking between tenants | EF query filters + PostgreSQL row-level security + non-owner DB role; architecture test that every tenant-owned entity is covered; a cross-tenant test for every endpoint |
| One tenant slows everyone ("noisy neighbour") | Per-tenant rate limits, query timeouts, background jobs queued per tenant, monitoring by tenant |
| Legacy registers are messy (duplicates, missing dates, unknown positions) | Import dry-run with a clean-up report; "unplaced" interments allowed until mapped |
| Poor or missing geodata | Schematic mode first, geometry later; grid generator cuts drawing time |
| Legal requirements differ by country and municipality | Rules as configurable per-tenant policies, validated with the pilot before Phase 2 ends |
| Map performance on phones with large cemeteries | Vector tiles, clustering, load tests in Phase 7 |
| Payment provider differences (Monri, PayPal, Payrexx) | Provider-agnostic payment service, one gateway class per provider, sandbox contract tests per provider |
| Personal data of holders and the bereaved | Privacy by default, audit log, GDPR records; reuse Matica's GDPR/FADP work |
