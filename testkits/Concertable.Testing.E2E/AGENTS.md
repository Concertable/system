# Concertable.Testing.E2E — shared E2E harness

## This project is SERVICE-AGNOSTIC. Nothing service-specific goes here. Ever.

This library holds only what **every** E2E suite needs, with no knowledge of any data service:

- Pins for **adapter services** (`PinAuthService`, `PinPaymentWeb`, `PinPaymentWorkers`, `PinStripeCli`) — Auth and Payment are adapters present in every host by architecture, so their pins are legitimately shared.
- Generic infra (`AddEphemeralSql`, `HealthWaiter`, `PollingService`, `TestTokenMinter`, `AspireResourceLogger`, the MSBuild tasks).

Things that must **never** be added here:

- **Per-service composition** (each suite's own `AddE2EStack`, `PinAuthApi`, `PinWeb`, …). These live in the owning suite: `Concertable.B2B.E2ETests` and `Concertable.Customer.E2ETests` each carry their own `DistributedApplicationBuilderExtensions`. Name these for their **role**, never by restating the suite (`AddE2EStack`, not `AddB2BE2E`) — the namespace already says which service; keep a prefix only when composing *another* service (`AddSearchService`, `PinPaymentWeb`).
- **Data-service helpers**, even when more than one suite consumes them. They get a helpers project owned by that service, referenced explicitly by the suites that need it — see `Concertable.Payment.E2ETests.Helpers` and `Concertable.Search.E2ETests.Helpers` (`AddSearchService`: both find pages are Search-backed, so both suites run Search by importing Search's own helpers project as an isolated dependency).
- Anything referencing a data service's runtime projects, types, or seed libraries.

The test for new code: *"would this file still compile and make sense if every data service moved to its own repo tomorrow?"* If a type, pin, or path in it names B2B, Customer, or Search, the answer is no — put it in that service's tree.

This rule has been violated and reverted before. Don't relitigate it: if a suite needs something service-specific, the suite (or the owning service's helpers project) is where it goes, even if that means two suites each writing three similar lines.

## Scenario-authoring rules

How to author a UI E2E scenario — one behaviour, start at the nearest already-verified state, fast-forward
via seeded state never UI replay, what cannot be seeded, headless by default — is the
**`e2e-scenarios` skill**. It applies to every suite here (B2B, Customer; Reqnroll + Playwright). The
mechanics that are ours:

- **CI is the only trusted pass/fail record** — the pipeline runs the whole suite, so read job history rather than any checked-in list. There is deliberately no local baseline file.
- **Run through `./scripts/e2e.ps1 ui <cmd>`** — never `dotnet test` directly; the script owns the mandatory
  Docker health gate. `-Headed` only when a human is watching.
- **A fast-forward `Given` reads `fixture.App.SeedState…`** and sets the id on scenario state. Each suite's
  own `AGENTS.md` adds its concrete `SeedState` shape.
- **The unseedable thing here is payment/Stripe state** — real Payment emits only on live Stripe webhooks, so
  no seeder creates a PaymentIntent or charge. Split any scenario whose assertion needs a real Stripe object.
- **Page objects, `data-testid` naming and step-binding shape** are
  [`E2E_UI_CONVENTIONS.md`](./E2E_UI_CONVENTIONS.md) — a page object owns every selector and Playwright call,
  a step binding owns none.
- **The traps that have cost real time here** are [`E2E_CONSIDERATIONS.md`](./E2E_CONSIDERATIONS.md) — chiefly
  that the 3DS off-session card is only challenged once per PaymentMethod *ever*, so a saved-card verify flow
  needs `CompleteChallengeIfRequiredAsync`, and that a timing-out wait is a signal to diagnose, never to widen.
