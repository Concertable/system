# Concertable.AppHost

**Executable umbrella Aspire host.** Runs every service in the system at once for "I want everything wired up" dev sessions.

This project has a `Program.cs` and an entry point. It references each service's generated `Projects.Concertable_*` metadata (B2B, Customer, Search, Auth, Payment — Web + Workers) plus the Aspire SDK, and composes them into a single `DistributedApplication`.

## What it is, what it isn't

- **It is** the place that runs the *whole* system locally.
- **It is not** the canonical dev experience for any single service. Each service has its own standalone executable AppHost (`Concertable.B2B.AppHost`, `Concertable.Customer.AppHost`, etc.) that runs that service in isolation. See the root [`ARCHITECTURE.md`](../../ARCHITECTURE.md) for why standalone is canonical.
- **It is not** the place to put per-service wiring (resource names, client IDs, secret keys, inter-service deps). That belongs in each service's `Concertable.X.AppHost.Extensions` library so both the umbrella here and the per-service standalone AppHost can compose it.

## Related projects

- [`../Concertable.AppHost.Shared/`](../Concertable.AppHost.Shared/README.md) — class library of reusable Aspire helpers consumed by this AppHost (and every per-service AppHost).
- `api/Concertable.X/Concertable.X.AppHost/` — per-service standalone executable AppHosts.
- `api/Concertable.X/Concertable.X.AppHost.Extensions/` — per-service extension libraries (topology contributions today; eventually resource registration too — see [`TECH_DEBT.md`](./TECH_DEBT.md)).
