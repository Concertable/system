# Concertable E2E — Technical Debt

When an item is fixed, update this file.

---

## HIGH

### API E2E suites are outside the CI E2E gate; UI scenarios stop at draft creation

Two facts compound into a coverage hole over the back half of the concert lifecycle:

1. The CI `e2e-ui-tests` job runs **Reqnroll UI scenarios only**. The four contract workflow features (`FlatFee`/`DoorSplit`/`VenueHire`/`VersusWorkflow.feature`) all end at draft creation + payment-card variants. No scenario posts a concert to the marketplace, finishes one, or settles a deferred contract — so "E2E green" never said anything about post → finish → settle → payout.
2. The API E2E tests that **do** cover that tail — `Concertable.B2B.E2ETests/Payments/ConcertDraftTests.cs` + `ConcertFinishedTests.cs` and `Concertable.Customer.E2ETests/Payments/TicketPurchaseTests.cs` — live in separate xUnit assemblies that no merge gate runs. Unrun tests rot: at current HEAD the DoorSplit/Versus `ConcertFinishedTests` cannot pass (finish dispatches on `concert.DealType`, which production never persisted), and nothing noticed.

Net effect: until the per-contract integration tests added 2026-06-06 (`Concertable.B2B.Concert.IntegrationTests/Concert/Concert*ApiTests.cs`), the post-accept lifecycle had **zero enforced coverage at any test level**. Three production bugs hid there: bookings and concert drafts persisted with `DealType = 0` (FlatFee) so finish/settle dispatched the wrong workflow, and the deferred-contract stage sequence blocked booking settlement outright.

**Resolves when:**

- The API E2E suites join an enforced run contract: a parallel api-e2e gate sits in the same merge checklist as the UI E2E job.
- `ConcertFinishedTests` asserts the per-contract *outcome*, not just intermediate artifacts — for DoorSplit/Versus that means booking `Complete` after the settlement webhook round-trip, not merely that a settlement payment intent exists.
- Until both hold, the integration-level `Concert*ApiTests` are the only finish/settle gate — keep them green.

### E2E stack assumes host prerequisites that only dev machines have

The Aspire E2E stack depends on tooling and config that every dev machine happens to provide but a clean runner does not, and nothing declares or installs them in one place. Each surfaced as a *separate* CI failure, peeled back one at a time (2026-06-15):

1. `appsettings.E2E.json` was gitignored + untracked — the fixtures couldn't even construct on a clean checkout (now tracked, secret-free).
2. The ASP.NET Core HTTPS dev cert — every dev runs `dotnet dev-certs https` once; CI didn't, so Kestrel couldn't bind `https://localhost:708x` (now provisioned + `SSL_CERT_DIR` in the workflow).
3. The `stripe` CLI binary — `AddStripeCli` runs `stripe listen` and Payment Web blocks 60s on its `whsec_` output; the runner had no `stripe` (now installed in the workflow).

This is also why `e2e-api-tests` (merge_group-only, added 2026-05-04) was **0/4 green** — a dev machine was the only environment it had ever run in, so the drift never showed until the queue ran it.

**Resolves when:**

- One provisioning path (a `setup` script or devcontainer) installs/configures the dev cert, the Stripe CLI, Node, etc., and **both** local dev and the CI jobs use it — so "works on my machine" and "works in CI" are the same machine.
- The E2E gate runs often enough (per-PR, not only `merge_group`) that this kind of drift is caught the day it's introduced, not months later.

---

## MEDIUM

### E2E fixture teardown is duplicated and can abandon later cleanup

The B2B and Customer `AppFixture` classes manually dispose the same categories of long-lived resources
in nested `try`/`finally` blocks. Most resources are outside the protected finalizers, so one failing
client, database, host, application, or logger disposal can prevent later resources from being cleaned
up. The duplication also makes acquisition and release order difficult to verify across the two suites.

**Resolves when:**

- Fixture initialization and teardown share a lifetime model that guarantees reverse-order cleanup,
  continues after individual disposal failures, and also handles partial initialization.
- The call sites remain ordinary resource construction rather than requiring a parallel cleanup callback
  registration beside every assignment.

### API E2E AppHosts still launch the frontend SPAs

The B2B/Customer AppHosts add the Vite SPAs (`AddVenueSpa`/`AddArtistSpa`/`AddBusinessSpa`, `AddCustomerSpa`) unconditionally, and the API E2E suites reuse those AppHosts. The API suites are headless (they drive services over HTTP and never open a browser), so the SPAs are dead weight — they `FailedToStart` in CI (no Node) and used to be awaited via `WaitForAllServingAsync`, which was removed 2026-06-15. They still sit in the resource graph as failed resources.

**Resolves when:**

- The API E2E composition (each suite's `AddE2EStack`) strips the `NodeAppResource` SPA resources, so the headless API stack doesn't launch frontends at all — mirroring how `AddEphemeralSql` tailors the dev AppHost for testing.

### WORKAROUND — 12-minute E2E health wait covers slow demo-user seeding

> Workaround, not a fix. Tagged `// WORKAROUND` at the call site
> (`Concertable.B2B`/`Concertable.Customer` `AppFixture.WaitForAllHealthyAsync`). Remove when the
> resolution below lands; until then every E2E run pays the inflated wait.

The 71 demo users are created one `CredentialRegisteredEvent` at a time (Auth registers a credential → outbox → ASB → B2B/Customer handler → insert user). On the CI runner's ASB emulator this reached only ~53/71 in 6 minutes, so the health wait — which gates on the `users` check — was bumped to 12 min (2026-06-15).

**Resolves when:**

- The credential seed is fast enough to finish well inside a 6-minute budget — e.g. tighten the outbox/inbox dispatch interval for the E2E environment, or batch the credential registration — at which point the timeout reverts to 6 and this entry is deleted.
