# R5 Integrations

Goal: the integrations the earlier phases recorded as settings actually run. Can move before the frontend redesign if a customer needs online payments first (open decision).

Gate: a webhook reaches a test receiver with a valid signature; one payment gateway completes a sandbox payment end to end.

---

## R5.1 Outbox + dispatcher

- Finding: B5
- Do: `outbox_messages` table (id, tenant_id, type, payload jsonb, occurred_at, processed_at, attempts, last_error). Aggregates raise domain events; `SaveChanges` writes them to the outbox in the same transaction (interceptor). A Hangfire recurring job dispatches unprocessed messages with retry and back-off.
- Done when: an interment writes one outbox row in the same transaction; a failing handler is retried and the error recorded.

## R5.2 Webhook delivery

- Do: outbox subscriber that POSTs matching events to each `WebhookSubscription` URL, HMAC-SHA256 signature header with the subscription secret, timestamp header, 10 s timeout, retries with back-off, delivery log visible to the clerk.
- Done when: an integration test with a local HTTP listener receives a signed `interment.recorded` event.

## R5.3 Payment gateway adapters

- Do: a `IPaymentGateway` port; adapters for Monri, PayPal and Payrexx (TWINT), each using the operator's own merchant account (`MerchantAccount`). Start with the one the pilot customer needs. Hosted payment page + signed callback endpoint; idempotent on the provider's transaction id.
- Done when: a sandbox payment marks the charge paid exactly once even if the callback arrives twice.

## R5.4 Matica sync

- Do: replace local-only `MaticaLink` rows with a call to Matica's API (contract to agree with the Matica repo). Until then, keep the feature labelled "manual link" in the UI.
- Done when: agreed with Miljenko which Matica endpoint to call.

---

## Found while working

- R5.4 is closed on the contract in `plan.md`: Cemetery exposes the link, and Matica calls it. The endpoint is `POST /api/v1/interments/{intermentId}/matica-links` with `{ "deathCertificateId" }`. Cemetery stores that id on a local `MaticaLink`. No Matica base URL exists, so Cemetery does not call out. The settings screen in [cemetery_frontend#20](https://github.com/miljenkostipic/cemetery_frontend/pull/20) labels the section as a manual link until Matica calls the endpoint.
- PayPal callbacks are checked with HMAC-SHA256 of the raw body and the merchant secret. PayPal's own webhooks are verified with a certificate, which this sandbox adapter does not check.
- The Payrexx adapter posts JSON with an `X-API-Signature` header. Payrexx's gateway API is form-encoded, so a live instance may reject the request.
- Hangfire is not registered while the host is headless. The dispatcher tests call `OutboxDispatcher` directly; the recurring job is `outbox-dispatch`, once a minute, only when Hangfire starts.
