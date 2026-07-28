# Concertable.AppHost — Technical Debt

When an item is fixed, update both this file and [`ARCHITECTURE.md`](./ARCHITECTURE.md).

`Concertable.AppHost` is the **umbrella** AppHost — "I want everything wired up at once." Standalone per-service AppHosts (`Concertable.X.AppHost`) are the canonical dev experience; the umbrella should be a thin composition of per-service extension libraries, not the place where service-specific wiring lives.

---

## HIGH

### Composition still goes through `AppHost.Shared` `Add*` wrappers

`Program.cs` calls `builder.AddAuth(...)`, `builder.AddApi(...)`, `builder.AddCustomerWeb(...)`, `builder.AddSearchWeb(...)`, `builder.AddPaymentWeb(...)`, etc. — all of which live in `Concertable.AppHost.Shared/DistributedApplicationBuilderExtensions.cs` and know each service's name, client ID, secrets, and inter-service deps. See [`../Concertable.AppHost.Shared/TECH_DEBT.md`](../Concertable.AppHost.Shared/TECH_DEBT.md) for the full description.

**Resolves when:** the per-service wiring moves into each `Concertable.X.AppHost.Extensions` library (mirroring the already-correct `AddXTopology()` pattern), `Program.cs` here composes those per-service extensions directly, and `AppHost.Shared` is reduced to truly generic helpers.

### `AppHost.Shared/Constants.cs` is a god-bucket of per-service constants

`api/Concertable.AppHost.Shared/Constants.cs` (`AppHostConstants`) holds `Databases.{Auth,B2B,Customer,Search,Payment}`, `ResourceNames.{B2BWeb, CustomerWeb, SearchWeb, SearchWorkers, Auth, PaymentWeb, PaymentWorkers, Workers, StripeCli, B2BSeedingSimulator}`, and `Ports.{B2BWeb, CustomerWeb, SearchWeb, PaymentWeb, Auth, CustomerSpa, VenueSpa, ArtistSpa, BusinessSpa}` -- all of which are per-service identifiers that belong to each service, not to a shared kernel. Every new service-specific resource (e.g. the recently added `B2BSeedingSimulator`) makes this worse. Cross-service consumers (each service's `E2ETests/AppFixture.cs`, `DbFixture.cs`) import the god-bucket to pick up the one or two constants they need.

**Resolves when:** each per-service constant moves into its owning `Concertable.X.AppHost.Extensions/XConstants.cs` (e.g. `B2BConstants.WebResource`, `B2BConstants.Database`, `B2BConstants.WebUrl`, `B2BConstants.SeedingSimulatorResource`). `AppHost.Shared` keeps only truly cross-service constants (if any). Consumers `using Concertable.B2B.AppHost.Extensions` to pick up B2B's names directly. Same split applies to `DistributedApplicationBuilderExtensions.cs` (see item above) -- the two refactors are best done in one pass.

---

## MED

### ASB topology hand-mirrors the runtime queue/topic naming as magic strings

The topology declarations (`AsbTopology` calls in each `Concertable.X.AppHost.Extensions` + `AuthTopology`) spell out entity names as string literals -- `command-concertable-auth-sendemailcommand`, `event-concertchangedevent`, etc. -- that must stay **exactly** in lock-step with the values the runtime computes in `Concertable.Messaging.AzureServiceBus`: `AzureServiceBusOptions.QueueNameFor(type)` (`command-{ServiceName}-{typeName}`) and `TopicNameFor(type)` (`event-{typeName}`). Nothing enforces the match. Rename a command/event type, or change a service's `ServiceName`, and the runtime name shifts while the literal does not -- a **silent** break: the receiver's `StartProcessingAsync` fails on a missing queue, or messages dead-letter against a queue nobody reads. Phase 1 of the async-email refactor declaring both `command-processstripewebhookcommand` and `command-concertable-payment-processstripewebhookcommand` by hand (then dropping one in phase 2) was exactly this convention-duplication being managed manually.

**Resolves when:** the queue/topic naming is a single shared source of truth used by both the runtime and the topology, and `AsbTopology` exposes type-driven declarations -- e.g. `.Command<SendEmailCommand>("concertable-auth")` and `.Event<ConcertChangedEvent>(subscription, group)` -- that compute the name via that same helper, so a rename cannot drift. Trade-off to weigh: this pulls the message-contract type references into the `*.AppHost.Extensions` projects (today they are string-only); decide whether that coupling is acceptable or whether a `nameof`-based middle ground is preferable.
