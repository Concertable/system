# Concertable.AppHost

**Executable umbrella Aspire host.** Runs every service in the system at once for "I want everything wired up" dev sessions.

This project uses the project-based Aspire convention: top-level composition in `AppHost.cs`. It references each service's generated `Projects.Concertable_*` metadata (B2B, Customer, Search, Auth, Payment — Web + Workers) plus the Aspire SDK, and composes them into a single `DistributedApplication`.

## What it is, what it isn't

- **It is** the place that runs the *whole* system locally.
- **It is not** the canonical dev experience for any single service. Each service has its own standalone executable AppHost (`Concertable.B2B.AppHost`, `Concertable.Customer.AppHost`, etc.) that runs that service in isolation. See the `microservice-boundaries` skill for why standalone is canonical (and the adapter-vs-data-service rule), and [`ARCHITECTURE.md`](./ARCHITECTURE.md) for this host's role as the full-system integration view.
- **It is not** the place to put per-service wiring (resource names, client IDs, secret keys, inter-service deps). That belongs in each service's `Concertable.X.AppHost.Extensions` library so both the umbrella here and the per-service standalone AppHost can compose it.

## Related projects

- `Concertable.AppHost.Shared` — published package of reusable Aspire helpers consumed by this AppHost and every per-service AppHost.
- `Concertable.<Service>.Hosting` — published per-service composition packages. Their container overloads are the only way this AppHost adds a service; the project overloads exist for the owning service's own standalone AppHost, which lives in that service's repository.
