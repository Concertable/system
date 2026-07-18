# Customer UI E2E (Reqnroll + Playwright) — service-specific authoring notes

**The scenario-authoring rules that apply to every suite** — test one behaviour, start at the nearest
already-verified state, fast-forward via seeded state (never UI replay), what can't be seeded
(payment/Stripe), and baseline discipline — live in `E2E_CONVENTIONS.md`, imported here:

@../../../../docs/E2E_CONVENTIONS.md

This file only adds Customer-specific mechanics.

## Fast-forward `Given`s read the Customer suite's `SeedState`

Setup jumps to state via seeded data off `fixture.App.SeedState` — no navigation — exactly as the B2B
suite does. A scenario acting on an existing concert/ticket starts from a seeded `Given` (e.g. `the
customer is on a concert detail page`), not by re-driving browse → search → open. If a scenario needs a
starting state that isn't seeded yet, add the seeded state + a `Given`; don't replay UI another scenario
already covers.

The one thing you cannot seed remains payment/Stripe state: a ticket-purchase confirmation that needs a
real charge/webhook must run the real paying flow — see the shared doc.
