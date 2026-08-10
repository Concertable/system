# Concertable.AppHost — Technical Debt

When an item is fixed, update both this file and [`ARCHITECTURE.md`](./ARCHITECTURE.md).

`Concertable.AppHost` is the **umbrella** AppHost — "I want everything wired up at once." Standalone per-service AppHosts (`Concertable.X.AppHost`) are the canonical dev experience; the umbrella should be a thin composition of per-service extension libraries, not the place where service-specific wiring lives.

---

## HIGH

### Composition still goes through `AppHost.Shared` `Add*` wrappers

`Program.cs` still calls B2B, SPA, and mobile `builder.Add*(...)` wrappers that live in `Concertable.AppHost.Shared/DistributedApplicationBuilderExtensions.cs` and know service-specific names, client IDs, secrets, and inter-service dependencies.

**Resolves when:** the per-service wiring moves into each service-owned `Concertable.X.Hosting` library, `Program.cs` here composes those hosting extensions directly, and `AppHost.Shared` is reduced to truly generic helpers. Hosting libraries are reusable Aspire composition capabilities, not executable AppHosts: their extension containers use relative names such as `AppHostExtensions`, rather than repeating the service name already carried by the project and namespace.

**Progress:** the canonical hosting pattern is established by Payment, Search, Customer, and B2B: `AddPaymentWeb`/`AddPaymentWorkers`/`AddStripeCli` in `Concertable.Payment.Hosting`, `AddSearchWeb`/`AddSearchWorkers` in `Concertable.Search.Hosting`, `AddCustomerWeb` in `Concertable.Customer.Hosting`, and `AddB2BWeb`/`AddB2BWorkers`/`AddB2BSeedingSimulator` in `Concertable.B2B.Hosting/AppHostExtensions.cs` (with `B2BConstants` + `B2BTopology`) — each in a `Concertable.X.Hosting/AppHostExtensions.cs`, consumed by every AppHost plus the E2E helpers. Extracting B2B emptied `AppHost.Shared/Constants.cs`, which is deleted. **Functionally extracted but still using the superseded project/type naming (migrate per follow-up PR):** Auth (`Concertable.Auth.AppHost.Extensions/AuthAppHostExtensions.cs`) becomes `Concertable.Auth.Hosting/AppHostExtensions.cs`. **Still in `AppHost.Shared`:** the SPA + mobile surfaces — `AddCustomerSpa`, `AddVenueSpa`, `AddArtistSpa`, `AddBusinessSpa`, `AddMobile`, `AddMobileB2B`, `AddMobileCustomer` — which share the private `AddSpaSurface`/`AddMobileSurface` helpers and are a frontend-composition concern, not a backend service; extracting them is the remaining step. The generic `AddSqlServerContainer`, `AddServiceBus`, `AddAzureStorage`, `Topology`, `WithOptionalEnvironment`, and `AddSecrets` helpers are cross-service and stay.
