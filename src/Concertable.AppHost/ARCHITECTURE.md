# Concertable.AppHost — architecture

The umbrella host's one job: **run the whole fleet together — every data service (`B2B`, `Customer`,
`Search`) plus the adapter services (`Auth`, `Payment`) and StripeCli — to model the fully-deployed
system.** It is the integration view, not a service's dev experience. For what the project *is*
(entry point, references, what doesn't belong here) see [`README.md`](./README.md); for the
microservice premise and the adapter-vs-data-service rule see [`api/ARCHITECTURE.md`](../ARCHITECTURE.md).

## What makes it different from a standalone host

A standalone service host (`Concertable.B2B.AppHost`, `Concertable.Customer.AppHost`) runs **one**
data service, plus the adapter services it requires (`Auth`, `Payment`), plus the seed simulators that
stand in for the data services it does *not* run. The umbrella runs **all** data services at once, so:

- It's the only host where B2B, Customer and Search run together against shared Auth + Payment.
- It wires **StripeCli** (`AddStripeCli(paymentWeb)`) so real test-mode Stripe payments drive real
  Payment events — the integrated, production-shaped payment flow.
- It does **not** register the seed simulators. They exist to supply events a *standalone* host is
  missing (B2B's events for Customer; seed payment events real Payment never emits). In the umbrella
  the real producers run and StripeCli drives real payments, so registering them would double-publish
  (see the `*.Seed.Simulator/CLAUDE.md` files and `plans/PAYMENT_SEED_CATALOG.md`).

## Startup waits are not special to this host

Cross-service `WaitFor` is decided by the adapter-vs-data rule, not by which host you're in. Adapter
waits (`WaitFor(auth)`, `WaitFor(paymentWeb)`) live in `Concertable.AppHost.Shared` and apply in
**every** host, because every data service genuinely requires Auth + Payment. What no host may do —
umbrella included — is make one data service `WaitFor` another; that coupling is forbidden and is
replaced by events + seed simulators. So this host adds **no** special `WaitFor` of its own; it differs
from a standalone host by *breadth* (all data services + StripeCli), not by wait ownership.

## Related

- [`README.md`](./README.md) — what this project is and isn't.
- [`api/ARCHITECTURE.md`](../ARCHITECTURE.md) — adapter-vs-data services and the microservice premise.
- [`../Concertable.AppHost.Shared/`](../Concertable.AppHost.Shared/README.md) — shared helpers (references + adapter-service waits).
- [`TECH_DEBT.md`](./TECH_DEBT.md).
