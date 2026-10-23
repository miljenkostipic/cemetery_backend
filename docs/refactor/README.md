# Refactor plan (post Phase 7)

Written 22–23 Oct 2026 after a review of both repos. Phases 0–7 shipped features, but the backend has gaps against `AGENTS.md`, and the frontend has no real UI structure. This folder is the work plan. Work through it in order.

The frontend half lives in `cemetery_frontend/docs/refactor/`, and the chosen design (direction C, "map at the centre") is in `cemetery_frontend/docs/design/`.

## How to work this plan (humans and AI agents)

1. Take the **first unchecked task** in the status table below. Never start a later step while an earlier step's gate is red.
2. Open its step file and do **exactly that task**. Nothing outside its "Do" list. If you find another problem, add it to "Found while working" at the end of the step file. Do not fix it.
3. One task = one branch = one PR. The branch is named after the task (`r1-3-layout-tenant-tests`).
4. Write or adjust the tests first, then the code. Every "Done when" check must pass locally before you tick the box.
5. Tick the task in this table **in the same PR**.
6. Do not change `AGENTS.md` rules, CI thresholds or tests just to make a task pass. If a rule blocks a task, stop and ask.

## Steps and gates

| Step | File | Gate before the next step |
| --- | --- | --- |
| R0 Stabilise | [R0-stabilise.md](R0-stabilise.md) | CI fails on OpenAPI drift and on coverage below target |
| R1 Safety net | [R1-safety-net.md](R1-safety-net.md) | Every endpoint has happy / 403 / cross-tenant tests; policy snapshot pinned |
| R2 Phase 4 + read models | [R2-phase4-and-read-models.md](R2-phase4-and-read-models.md) | The frontend's OpenAPI has 0 paths this API lacks |
| R3 Frontend foundation | frontend repo | Map screen runs on real `/map` and `/dossier` |
| R4 Screens on design C | frontend repo | Playwright + axe green on 3 flows |
| R5 Integrations | [R5-integrations.md](R5-integrations.md) | Webhooks delivered, one gateway live in sandbox |

## Status

| Task | Title | Finding | Done |
| --- | --- | --- | --- |
| R0.1 | Merge `fix-payments-migration-order` into `main` | B10 | [ ] |
| R0.2 | OpenAPI export + drift check in CI | B8 | [ ] |
| R0.3 | Coverage thresholds in CI | B9 | [ ] |
| R0.4 | Register handlers by assembly scan | B7 | [ ] |
| R1.1 | Shared `ApiFixture` for integration tests | B2 | [ ] |
| R1.2 | One access policy and `ICallerContext` | B3 | [ ] |
| R1.3 | Endpoint tests: Auth, Organizations, Dashboard | B2 | [ ] |
| R1.4 | Endpoint tests: Layout | B2 | [ ] |
| R1.5 | Endpoint tests: Register | B2 | [ ] |
| R1.6 | Endpoint tests: Public catalog + visibility | B2 | [ ] |
| R1.7 | Endpoint tests: Operations | B2 | [ ] |
| R1.8 | Endpoint tests: Billing | B2 | [ ] |
| R1.9 | Endpoint tests: Reports, Launch, Rights | B2 | [ ] |
| R1.10 | RLS sweep test over every tenant table | B2 | [ ] |
| R1.11 | Enums instead of string statuses | B6 | [ ] |
| R2.1 | Holder aggregate | B1 | [ ] |
| R2.2 | Grave right: grant, renew, transfer, inherit | B1 | [ ] |
| R2.3 | Reservations | B1 | [ ] |
| R2.4 | Site documents | B1 | [ ] |
| R2.5 | Holder portal endpoints (`/me/...`) | B1 | [ ] |
| R2.6 | Charges and permits reference `HolderId` | B1 | [ ] |
| R2.7 | Persisted occupancy on grave sites | B4 | [ ] |
| R2.8 | Paging + filters on lists | B4 | [ ] |
| R2.9 | `GET /cemeteries/{id}/map` | API-C | [ ] |
| R2.10 | `GET /grave-sites/{id}/dossier` | API-C | [ ] |
| R2.11 | `GET /search` | API-C | [ ] |
| R2.12 | Entrances, paths, extended public memorial | API-C | [ ] |
| R5.1 | Outbox + Hangfire dispatcher | B5 | [ ] |
| R5.2 | Webhook delivery | B5 | [ ] |
| R5.3 | Payment gateway adapters | B5 | [ ] |
| R5.4 | Matica sync | B5 | [ ] |

Findings with evidence: [findings.md](findings.md).

## Open decisions (owner: Miljenko)

- Holder model: a person record that may or may not have a login (assumed in R2.1), or always a user account?
- Largest cemetery expected in year one. Under about 5,000 sites, GeoJSON is enough and vector tiles wait.
- Will clerks draw entrances and paths? Public directions (R2.12) depend on them.
- R5 before or after the frontend redesign?
