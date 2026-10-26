# Refactor issues

One checkbox per task, in the order to do them. This file is the single source of truth for progress.

How to use it with Cursor:

1. Point Cursor at the next unchecked item: *"Do the next unchecked task in docs/refactor/ISSUES.md."*
2. Cursor reads the linked task section and does only that task (see `.cursor/rules/refactor-plan.mdc`).
3. When every "Done when" check passes, the same change ticks the box here (`- [x]`) and fills in the PR link.
4. Review the PR against the task's "Done when" list before merging. Do not start the next step until its gate (see README) is green.


## R0 Stabilise

Gate: CI fails on OpenAPI drift and on coverage below target

- [ ] **R0.1** Merge `fix-payments-migration-order` into `main` · finding B10 · [task](R0-stabilise.md#r01-merge-the-payments-migration-fix) · PR: 
- [x] **R0.2** OpenAPI export + drift check in CI · finding B8 · [task](R0-stabilise.md#r02-openapi-export--drift-check) · PR: 
- [ ] **R0.3** Coverage thresholds in CI · finding B9 · [task](R0-stabilise.md#r03-coverage-thresholds-in-ci) · PR: 
- [x] **R0.4** Register handlers by assembly scan · finding B7 · [task](R0-stabilise.md#r04-register-handlers-by-assembly-scan) · PR: 

## R1 Safety net

Gate: Every endpoint has happy / 403 / cross-tenant tests; policy snapshot pinned

- [ ] **R1.1** Shared `ApiFixture` for integration tests · finding B2 · [task](R1-safety-net.md#r11-shared-apifixture) · PR: 
- [ ] **R1.2** One access policy and `ICallerContext` · finding B3 · [task](R1-safety-net.md#r12-one-access-policy-and-icallercontext) · PR: 
- [ ] **R1.3** Endpoint tests: Auth, Organizations, Dashboard · finding B2 · [task](R1-safety-net.md#r13r19-endpoint-tests-per-group) · PR: 
- [ ] **R1.4** Endpoint tests: Layout · finding B2 · [task](R1-safety-net.md#r13r19-endpoint-tests-per-group) · PR: 
- [ ] **R1.5** Endpoint tests: Register · finding B2 · [task](R1-safety-net.md#r13r19-endpoint-tests-per-group) · PR: 
- [ ] **R1.6** Endpoint tests: Public catalog + visibility · finding B2 · [task](R1-safety-net.md#r13r19-endpoint-tests-per-group) · PR: 
- [ ] **R1.7** Endpoint tests: Operations · finding B2 · [task](R1-safety-net.md#r13r19-endpoint-tests-per-group) · PR: 
- [ ] **R1.8** Endpoint tests: Billing · finding B2 · [task](R1-safety-net.md#r13r19-endpoint-tests-per-group) · PR: 
- [ ] **R1.9** Endpoint tests: Reports, Launch, Rights · finding B2 · [task](R1-safety-net.md#r13r19-endpoint-tests-per-group) · PR: 
- [ ] **R1.10** RLS sweep test over every tenant table · finding B2 · [task](R1-safety-net.md#r110-rls-sweep) · PR: 
- [ ] **R1.11** Enums instead of string statuses · finding B6 · [task](R1-safety-net.md#r111-enums-instead-of-string-statuses) · PR: 

## R2 Phase 4 and read models

Gate: The frontend's OpenAPI has 0 paths this API lacks

- [ ] **R2.1** Holder aggregate · finding B1 · [task](R2-phase4-and-read-models.md#r21-holder-aggregate) · PR: 
- [ ] **R2.2** Grave right: grant, renew, transfer, inherit · finding B1 · [task](R2-phase4-and-read-models.md#r22-grave-right) · PR: 
- [ ] **R2.3** Reservations · finding B1 · [task](R2-phase4-and-read-models.md#r23-reservations) · PR: 
- [ ] **R2.4** Site documents · finding B1 · [task](R2-phase4-and-read-models.md#r24-site-documents) · PR: 
- [ ] **R2.5** Holder portal endpoints (`/me/...`) · finding B1 · [task](R2-phase4-and-read-models.md#r25-holder-portal-endpoints) · PR: 
- [ ] **R2.6** Charges and permits reference `HolderId` · finding B1 · [task](R2-phase4-and-read-models.md#r26-charges-and-permits-reference-holderid) · PR: 
- [ ] **R2.7** Persisted occupancy on grave sites · finding B4 · [task](R2-phase4-and-read-models.md#r27-persisted-occupancy) · PR: 
- [ ] **R2.8** Paging + filters on lists · finding B4 · [task](R2-phase4-and-read-models.md#r28-paging-and-filters) · PR: 
- [ ] **R2.9** `GET /cemeteries/{id}/map` · finding API-C · [task](R2-phase4-and-read-models.md#r29-get-cemeteriesidmap) · PR: 
- [ ] **R2.10** `GET /grave-sites/{id}/dossier` · finding API-C · [task](R2-phase4-and-read-models.md#r210-get-grave-sitesiddossier) · PR: 
- [ ] **R2.11** `GET /search` · finding API-C · [task](R2-phase4-and-read-models.md#r211-get-search) · PR: 
- [ ] **R2.12** Entrances, paths, extended public memorial · finding API-C · [task](R2-phase4-and-read-models.md#r212-entrances-paths-extended-public-memorial) · PR: 

## R5 Integrations

Gate: Webhooks delivered, one gateway live in sandbox

- [ ] **R5.1** Outbox + Hangfire dispatcher · finding B5 · [task](R5-integrations.md#r51-outbox--dispatcher) · PR: 
- [ ] **R5.2** Webhook delivery · finding B5 · [task](R5-integrations.md#r52-webhook-delivery) · PR: 
- [ ] **R5.3** Payment gateway adapters · finding B5 · [task](R5-integrations.md#r53-payment-gateway-adapters) · PR: 
- [ ] **R5.4** Matica sync · finding B5 · [task](R5-integrations.md#r54-matica-sync) · PR: 
