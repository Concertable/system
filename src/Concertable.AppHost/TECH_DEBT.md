# Concertable.AppHost — Technical Debt

When an item is fixed, update both this file and [`ARCHITECTURE.md`](./ARCHITECTURE.md).

`Concertable.AppHost` is the **umbrella** AppHost — "I want everything wired up at once." Standalone per-service AppHosts (`Concertable.X.AppHost`) are the canonical dev experience; the umbrella should be a thin composition of per-service extension libraries, not the place where service-specific wiring lives.

---

## HIGH

### Composition still goes through `AppHost.Shared` `Add*` wrappers

`Program.cs` calls `builder.AddAuth(...)`, `builder.AddApi(...)`, `builder.AddPaymentWeb(...)`, etc. — all of which live in `Concertable.AppHost.Shared/DistributedApplicationBuilderExtensions.cs` and know each service's name, client ID, secrets, and inter-service deps.

**Resolves when:** the per-service wiring moves into each `Concertable.X.AppHost.Extensions` library (mirroring the already-correct `AddXTopology()` pattern), `Program.cs` here composes those per-service extensions directly, and `AppHost.Shared` is reduced to truly generic helpers.

**Progress:** the canonical pattern is established. **Done:** Search (`AddSearchWeb`/`AddSearchWorkers` in `Concertable.Search.AppHost.Extensions/SearchAppHostExtensions.cs`) and Customer (`AddCustomerWeb` in `Concertable.Customer.AppHost.Extensions/CustomerAppHostExtensions.cs`) — both AppHosts + each service's E2E helpers resolve them from there; the generic `WithOptionalEnvironment` helper is now `public` in `AppHost.Shared` for per-service libs to compose. **Remaining (every per-service wrapper still in `AppHost.Shared`, one service per follow-up PR):** Auth — `AddAuth`; B2B — `AddApi`, `AddWorkers`, `AddB2BSeedingSimulator`, `AddMobileB2B`; Payment — `AddPaymentWeb`, `AddPaymentWorkers`, `AddStripeCli`; SPAs — `AddCustomerSpa`, `AddVenueSpa`, `AddArtistSpa`, `AddBusinessSpa`; mobile — `AddMobile`, `AddMobileCustomer`. The generic helpers `AddSqlServerContainer`, `AddServiceBus`, `AddAzureStorage`, `Topology`, and `WithOptionalEnvironment` are cross-service and stay.

### `AppHost.Shared/Constants.cs` is a god-bucket of per-service constants

`api/Concertable.AppHost.Shared/Constants.cs` (`AppHostConstants`) still holds `Databases.{Auth,B2B,Payment}` and `ResourceNames.{B2BWeb, Auth, PaymentWeb, PaymentWorkers, Workers, StripeCli, B2BSeedingSimulator}` -- all per-service identifiers that belong to each service, not to a shared kernel. Every new service-specific resource (e.g. the recently added `B2BSeedingSimulator`) makes this worse. Cross-service consumers (each service's `E2ETests/AppFixture.cs`, `DbFixture.cs`) import the god-bucket to pick up the one or two constants they need.

**Resolves when:** each per-service constant moves into its owning `Concertable.X.AppHost.Extensions/XConstants.cs` (e.g. `B2BConstants.WebResource`, `B2BConstants.Database`, `B2BConstants.WebUrl`, `B2BConstants.SeedingSimulatorResource`). `AppHost.Shared` keeps only truly cross-service constants (if any). Consumers `using Concertable.B2B.AppHost.Extensions` to pick up B2B's names directly. Same split applies to `DistributedApplicationBuilderExtensions.cs` (see item above) -- do a service's constants and wrappers in one pass (they're coupled: a shared wrapper reads that service's constants).

**Progress:** **Search** and **Customer** are done — Search's constants live in `Concertable.Search.AppHost.Extensions/SearchConstants.cs` (`Database`, `WebResource`, `WorkersResource`, `ServiceName`), Customer's in `Concertable.Customer.AppHost.Extensions/CustomerConstants.cs` (`Database`, `WebResource`, `ServiceName`); the dead `Ports` class (0 references) was deleted outright. **Remaining (all entries still in `AppHostConstants`, split with their service's wrappers above):** `Databases.{Auth, B2B, Payment}`; `ResourceNames.{Auth, B2BWeb, Workers, B2BSeedingSimulator, PaymentWeb, PaymentWorkers, StripeCli}`; `ServiceNames.{Auth, B2B, Payment}`.
