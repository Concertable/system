# Concertable.Testing.E2E — shared E2E harness

## This project is SERVICE-AGNOSTIC. Nothing service-specific goes here. Ever.

This library holds only what **every** E2E suite needs, with no knowledge of any data service:

- Pins for **adapter services** (`PinAuthService`, `PinPaymentWeb`, `PinPaymentWorkers`, `PinStripeCli`) — Auth and Payment are adapters present in every host by architecture, so their pins are legitimately shared.
- Generic infra (`AddEphemeralSql`, `HealthWaiter`, `PollingService`, `TestTokenMinter`, `AspireResourceLogger`, the MSBuild tasks).

Things that must **never** be added here:

- **Per-service composition** (`AddB2BE2E`, `AddCustomerE2E`, `PinB2BWeb`, `PinCustomerWeb`, …). These live in the owning suite: `Concertable.B2B.E2ETests` and `Concertable.Customer.E2ETests` each carry their own `DistributedApplicationBuilderExtensions`.
- **Data-service helpers**, even when more than one suite consumes them. They get a helpers project owned by that service, referenced explicitly by the suites that need it — see `Concertable.Payment.E2ETests.Helpers` and `Concertable.Search.E2ETests.Helpers` (`AddSearchService`: both find pages are Search-backed, so both suites run Search by importing Search's own helpers project as an isolated dependency).
- Anything referencing a data service's runtime projects, types, or seed libraries.

The test for new code: *"would this file still compile and make sense if every data service moved to its own repo tomorrow?"* If a type, pin, or path in it names B2B, Customer, or Search, the answer is no — put it in that service's tree.

This rule has been violated and reverted before. Don't relitigate it: if a suite needs something service-specific, the suite (or the owning service's helpers project) is where it goes, even if that means two suites each writing three similar lines.

## Scenario-authoring rules

The rules for authoring UI E2E scenarios (test one behaviour, start at the nearest already-verified
state, fast-forward via seeded state never UI replay, what can't be seeded, baseline discipline) are
shared across every suite and live in [`E2E_CONVENTIONS.md`](../../../agents/E2E_CONVENTIONS.md) —
imported here so they're always in context when working in this harness:

@../../../agents/E2E_CONVENTIONS.md
