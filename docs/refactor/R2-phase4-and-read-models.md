# R2 Phase 4 and read models

Goal: the backend implements what the frontend already calls (Phase 4, `plan.md` "Rights and holders"), and serves the read models design C needs.

Gate: comparing `paths` in `cemetery_frontend/openapi/cemetery.json` with this repo's `openapi/cemetery.json` shows **0 paths that exist only in the frontend**.

Contract source: the 12 missing endpoints and their schemas (`GrantRightBody`, `SiteRightsView`, `GraveRightView`, `RenewRightBody`, `ConveyRightBody`, `ReservationBody`, `ReservationView`, `DocumentBody`, `DocumentView`, `HolderSiteView`, `HolderBody`) are already defined in the frontend's `openapi/cemetery.json`. Implement those shapes. If a shape must change, change the frontend in a matching PR and say so in both PR descriptions.

Domain rules from `plan.md` that these tasks must enforce:

- A grave site has at most one active grave right; holders' shares are recorded on it (rule 3).
- An interment needs, where required, the holder's consent (rule 1).
- Living holders' personal data is never public (rule 5). Never return holder data from `/public/*`.

Every new endpoint ships with the three R1 tests (happy, wrong role, other tenant) and goes in the policy snapshot.

---

## R2.1 Holder aggregate

- Finding: B1
- Assumption (open decision): a holder is a person record that may or may not have a login.
- Do: `Rights/Holder` (TenantEntity): name, OIB (optional, validated), address, email, phone, optional `UserId` link. EF config, RLS policy, migration. Clerk CRUD endpoints `/holders` (paged, search by name/OIB). Linking: inviting a holder creates an `Invitation` with role GraveHolder; accepting sets `Holder.UserId`.
- Done when: a clerk creates and finds a holder; a GraveHolder user sees only their own holder record.

## R2.2 Grave right

- Do: `Rights/GraveRight`: site, holders with shares (sum = 1), `ValidFrom`, `ValidTo` (null = perpetual), status (active, ended). Commands: grant (`GET/PUT /grave-sites/{id}/right` per frontend contract), renew, transfer (contract), inherit. Ending a right keeps history. Rule: at most one active right per site (domain + unique partial index).
- Done when: the domain tests cover share sums, overlap refusal, renewal and transfer; endpoints match the frontend schemas.

## R2.3 Reservations

- Do: `Rights/Reservation` for an available site: holder, until date; site status `reserved` while active; cancel endpoint. A reservation blocks interments by others.
- Done when: reserving changes the site status on `/map` (after R2.7) and cancelling restores it.

## R2.4 Site documents

- Do: `SiteDocument` on a site or a right: kind (contract, permit, scan, other), file in the existing storage port, max size and allowed MIME types validated server side, virus-scan hook as a port (no-op implementation for now). Download through an authorized endpoint only, never a public URL.
- Done when: upload, list and download work for a clerk and for the site's holder; another tenant gets 404.

## R2.5 Holder portal endpoints

- Do: `/me/grave-sites`, `/me/documents`, `/me/charges`, `/me/charges/{id}/payments`, `/me/payments/{id}` (Policy `Holder`), returning only data linked to the caller's `Holder`.
- Done when: a holder with two sites in tenant A sees both and nothing from a second holder.

## R2.6 Charges and permits reference `HolderId`

- Do: `Charge` and `MonumentPermit` get `HolderId` (nullable during migration). Keep `HolderName` as a snapshot. Migration backfills nothing automatically; add a clerk-run match tool later (note it in "Found while working"). The yearly charge run bills the active right's holders.
- Done when: a yearly run on a site with a right creates charges with `HolderId` set.

## R2.7 Persisted occupancy

- Finding: B4
- Do: store `used`, `status` and `rest_until` on `GraveSite`, updated in the same transaction by interment, exhumation, transfer, reservation and right changes (domain method, not a trigger). One-off migration computes the current values. `ListGraveSitesHandler` stops loading interments.
- Done when: a test records an interment and reads the new status from the site row; `SiteOccupancy.Summarize` is used only when writing.

## R2.8 Paging and filters

- Finding: B4
- Do: `PageRequest(cursor, limit ≤ 100)` / `Page<T>(items, nextCursor)`, keyset on `(sort key, id)`. Apply to deceased/register, interments, charges, arrears, work orders, holders, office requests. Filter records per list (for example `DeceasedFilter(q, sectionId, diedFrom, diedTo, withoutSite)`), reused by exports.
- Done when: register list of 10,000 seeded rows returns page 1 in < 100 ms on the test container; frontend contract regenerated.

## R2.9 `GET /cemeteries/{id}/map`

- Do: one SQL query building a GeoJSON FeatureCollection: sections, sites (`id`, `code`, `kind`, `status`, `used`, `capacity`, `hidden`), entrances, paths; plus `counts` per status. Weak ETag from the max `updated_at`; honour `If-None-Match` (304). Policy `Office`.
- Done when: a 5,000-site cemetery returns in < 300 ms and < 1.5 MB gzipped; second call with the ETag returns 304.

## R2.10 `GET /grave-sites/{id}/dossier`

- Do: site, occupancy slots, interments (name, years, buried on), current right + holders (name only for Field role), this year's fee status, primary photo URL (latest completed work-order photo), open work orders.
- Done when: one call serves the grave panel; the field-role test proves holder contact data is not returned.

## R2.11 `GET /search`

- Do: `q` (min 2 chars), optional `cemeteryId`; typed hits `deceased | site | holder`, each with `siteId` and centroid; `unaccent` + `pg_trgm` indexes; max 20 hits. Holder hits only for Office roles.
- Done when: "kovacevic" finds "Kovačević"; a FieldWorker never gets holder hits.

## R2.12 Entrances, paths, extended public memorial

- Do: layout layers `Entrance` (point, name) and `Path` (line) with clerk CRUD (drawn in the map's Edit mode). `/public/memorials/{id}` adds site centroid, section name, nearest entrance and others buried in the same grave (only those publicly visible).
- Done when: the public memorial returns the nearest entrance; a hidden deceased in the same grave is not listed.

---

## Found while working

(add items here, do not fix them in this step)
