# Concertable.AppHost — Technical Debt

When an item is fixed, update both this file and [`ARCHITECTURE.md`](./ARCHITECTURE.md).

`Concertable.AppHost` is the **umbrella** AppHost — "I want everything wired up at once." Standalone per-service AppHosts (`Concertable.X.AppHost`) are the canonical dev experience; the umbrella should be a thin composition of per-service extension libraries, not the place where service-specific wiring lives.

---

## HIGH

### Composition still goes through `AppHost.Shared` `Add*` wrappers

`Program.cs` still calls B2B, SPA, and mobile `builder.Add*(...)` wrappers that live in `Concertable.AppHost.Shared/DistributedApplicationBuilderExtensions.cs` and know service-specific names, client IDs, secrets, and inter-service dependencies.

**Resolves when:** the per-service wiring moves into each service-owned `Concertable.X.Hosting` library, `Program.cs` here composes those hosting extensions directly, and `AppHost.Shared` is reduced to truly generic helpers. Hosting libraries are reusable Aspire composition capabilities, not executable AppHosts: their extension containers use relative names such as `AppHostExtensions`, rather than repeating the service name already carried by the project and namespace.

**Progress:** the canonical hosting pattern is established by Payment and Search: `AddPaymentWeb`/`AddPaymentWorkers`/`AddStripeCli` live in `Concertable.Payment.Hosting/AppHostExtensions.cs` and `AddSearchWeb`/`AddSearchWorkers` in `Concertable.Search.Hosting/AppHostExtensions.cs`, each consumed by every AppHost plus the E2E helpers. **Functionally extracted but still using the superseded project/type naming (migrate one service per follow-up PR):** Customer (`Concertable.Customer.AppHost.Extensions/CustomerAppHostExtensions.cs`) and Auth (`Concertable.Auth.AppHost.Extensions/AuthAppHostExtensions.cs`); each becomes `Concertable.X.Hosting/AppHostExtensions.cs`. **Still in `AppHost.Shared` (extract directly to the hosting pattern):** B2B — `AddApi`, `AddWorkers`, `AddB2BSeedingSimulator`, `AddMobileB2B`; SPAs — `AddCustomerSpa`, `AddVenueSpa`, `AddArtistSpa`, `AddBusinessSpa`; mobile — `AddMobile`, `AddMobileCustomer`. The generic `AddSqlServerContainer`, `AddServiceBus`, `AddAzureStorage`, `Topology`, `WithOptionalEnvironment`, and `AddSecrets` helpers are cross-service and stay.

### `AppHost.Shared/Constants.cs` is a god-bucket of per-service constants

`api/Concertable.AppHost.Shared/Constants.cs` (`AppHostConstants`) still holds `Databases.{B2B,Payment}` and `ResourceNames.{B2BWeb, PaymentWeb, PaymentWorkers, Workers, StripeCli, B2BSeedingSimulator}` -- all per-service identifiers that belong to each service, not to a shared kernel. Every new service-specific resource (e.g. the recently added `B2BSeedingSimulator`) makes this worse. Cross-service consumers (each service's `E2ETests/AppFixture.cs`, `DbFixture.cs`) import the god-bucket to pick up the one or two constants they need.

**Resolves when:** each per-service constant moves into its owning `Concertable.X.Hosting/XConstants.cs` (e.g. `B2BConstants.WebResource`, `B2BConstants.Database`, `B2BConstants.WebUrl`, `B2BConstants.SeedingSimulatorResource`). `AppHost.Shared` keeps only truly cross-service constants (if any). Consumers import the owning service's Hosting namespace to pick up its names directly. Same split applies to `DistributedApplicationBuilderExtensions.cs` (see item above) -- do a service's constants and wrappers in one pass (they're coupled: a shared wrapper reads that service's constants).

**Progress:** **Search**, **Customer**, **Auth**, and **Payment** are out of the shared god-bucket — Search's constants live in `Concertable.Search.Hosting/SearchConstants.cs` (`Database`, `WebResource`, `WorkersResource`, `ServiceName`), Customer's in `Concertable.Customer.AppHost.Extensions/CustomerConstants.cs` (`Database`, `WebResource`, `ServiceName`), Auth's in `Concertable.Auth.AppHost.Extensions/AuthConstants.cs` (`Database`, `Resource`, `ServiceName`), and Payment's in `Concertable.Payment.Hosting/PaymentConstants.cs` (`Database`, `WebResource`, `WorkersResource`, `StripeCliResource`, `ServiceName`). Customer and Auth move with their hosting-library renames tracked above; the dead `Ports` class (0 references) was deleted outright. **Remaining (all entries still in `AppHostConstants`, split with their service's wrappers above):** `Databases.B2B`; `ResourceNames.{B2BWeb, Workers, B2BSeedingSimulator}`; `ServiceNames.B2B`.
