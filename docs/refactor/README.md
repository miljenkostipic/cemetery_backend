# Refactor plan (post Phase 7)

Written 22–23 Oct 2026 after a review of both repos. Phases 0–7 shipped features, but the backend has gaps against `AGENTS.md`, and the frontend has no real UI structure. This folder is the work plan. Work through it in order.

The frontend half lives in `cemetery_frontend/docs/refactor/`, and the chosen design (direction C, "map at the centre") is in `cemetery_frontend/docs/design/`.

## How to work this plan (humans and AI agents)

1. Take the **first unchecked task** in [ISSUES.md](ISSUES.md). Never start a later step while an earlier step's gate is red.
2. Open its step file and do **exactly that task**. Nothing outside its "Do" list. If you find another problem, add it to "Found while working" at the end of the step file. Do not fix it.
3. One task = one branch = one PR. The branch is named after the task (`r1-3-layout-tenant-tests`).
4. Write or adjust the tests first, then the code. Every "Done when" check must pass locally before you tick the box.
5. Tick the task in [ISSUES.md](ISSUES.md) **in the same PR** and add the PR link.
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

Progress is tracked in [ISSUES.md](ISSUES.md): one checkbox per task, in order.

Findings with evidence: [findings.md](findings.md).

## Open decisions (owner: Miljenko)

- Holder model: a person record that may or may not have a login (assumed in R2.1), or always a user account?
- Largest cemetery expected in year one. Under about 5,000 sites, GeoJSON is enough and vector tiles wait.
- Will clerks draw entrances and paths? Public directions (R2.12) depend on them.
- R5 before or after the frontend redesign?
