# Concertable.AppHost — Technical Debt

When an item is fixed, update both this file and [`ARCHITECTURE.md`](./ARCHITECTURE.md).

`Concertable.AppHost` is the **umbrella** AppHost — "I want everything wired up at once." Standalone per-service AppHosts (`Concertable.X.AppHost`) are the canonical dev experience; the umbrella should be a thin composition of per-service extension libraries, not the place where service-specific wiring lives.

---

## LOW

### Auth hosting library still uses the superseded `AppHost.Extensions` project/type naming

Every other host-composition library follows the canonical pattern — a `Concertable.X.Hosting` project exposing `AppHostExtensions` (`AddPaymentWeb`/`AddStripeCli` in Payment, `AddSearchWeb` in Search, `AddCustomerWeb` in Customer, `AddB2BWeb`/`AddB2BWorkers` in B2B), and the frontend surfaces now live in `Concertable.Frontend.Hosting` (`AddCustomerSpa`/`AddVenueSpa`/`AddArtistSpa`/`AddBusinessSpa`, `AddMobile`/`AddMobileB2B`/`AddMobileCustomer`). `AppHost.Shared` is now reduced to the truly generic cross-service helpers (`AddSqlServerContainer`, `AddServiceBus`, `AddAzureStorage`, `Topology`, `WithOptionalEnvironment`, `AddSecrets`). Auth alone is still `Concertable.Auth.AppHost.Extensions` exposing `AuthAppHostExtensions` — functionally extracted but off-pattern (the type restates the service name its project and namespace already carry).

**Resolves when:** `Concertable.Auth.AppHost.Extensions/AuthAppHostExtensions.cs` becomes `Concertable.Auth.Hosting/AppHostExtensions.cs`, with all references and `.slnx` entries updated.
