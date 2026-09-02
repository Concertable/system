# Concertable.AppHost — Technical Debt

When an item is fixed, update both this file and [`ARCHITECTURE.md`](./ARCHITECTURE.md).

`Concertable.AppHost` is the **umbrella** AppHost — "I want everything wired up at once." Standalone per-service AppHosts (`Concertable.X.AppHost`) are the canonical dev experience; the umbrella should be a thin composition of per-service extension libraries, not the place where service-specific wiring lives.

No outstanding debt.
