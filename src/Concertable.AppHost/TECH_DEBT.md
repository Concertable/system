# Concertable.AppHost — Technical Debt

When an item is fixed, update both this file and [`ARCHITECTURE.md`](./ARCHITECTURE.md).

`Concertable.AppHost` is the **umbrella** AppHost — "I want everything wired up at once." Standalone per-service AppHosts (`Concertable.X.AppHost`) are the canonical dev experience; the umbrella should be a thin composition of per-service extension libraries, not the place where service-specific wiring lives.

## Auth's endpoint declaration and the endpoint name belong in the Hosting packages

`AppHost.cs` still declares the `"https"`-named plaintext endpoint for Auth. B2B, Customer, Search and
Payment now each declare their own inside their `Concertable.<Service>.Hosting` package, with an
`ImageCompositionTests` suite so the omission cannot recur; `AddAuth`'s image overload is the last one
that leaves it to the caller. This composition should not know any image's port.

The endpoint name is the same problem one level down. `"https"` is retyped in `AddAuth`, `AddB2BWeb`,
`AddCustomerWeb`, `AddSearchWeb`, `AddPaymentWeb`, here, and in the black-box suite, with nothing tying
them together — which is how four of those packages came to never declare the endpoint the fifth
resolves through `GetEndpoint("https")`, leaving DCP unable to apply the Auth container's configuration
and reporting no reason for it. It is composition vocabulary like `AuthConstants.Resource` or
`PaymentConstants.GrpcPort`, so it belongs beside them in `Concertable.AppHost.Shared`. Neither
Aspire's `KnownEndpointNames` (internal) nor `Uri.UriSchemeHttps` (a URI scheme, not an endpoint name)
can stand in for it.

The name also has a cost the constant should carry a warning about: Aspire keys service discovery for an
endpoint named `http` or `https` by the endpoint's **scheme**, not its name
(`EndpointReference.IsHttpSchemeNamedEndpoint`), so a plaintext endpoint named `https` publishes
`services:<resource>:http:0` and no `https:0` key ever exists. Consumers that resolve by URL through
`GetEndpoint("https")` are unaffected; a consumer using real .NET service discovery must resolve a name
that is not an http scheme, which is why `Concertable.Payment.Client` reads
`services:payment-web:grpc:0`.

Both are one producer change plus a publish and a re-pin here.

## Four services still publish composition identities from a catch-all constants class

B2B retired `B2BConstants` in its PostgreSQL cut-over, so its identities now come from focused types —
`B2BDatabase.Name`, `B2BWeb.Name`, `B2BWorkers.Name`, `B2BMigrations.Name`, `B2BSeedingSimulator.Name`.
Payment, Search, Customer and Auth never followed, so this composition and its black-box fixture read
`B2BMigrations.Name` on one line and `PaymentConstants.MigrationsResource`,
`SearchConstants.MigrationsResource`, `CustomerConstants.WebResource` and `AuthConstants.Database` on the
next. A catch-all carrying a database name, a resource name, a port and an environment-variable name says
nothing about which of those a caller is asking for, and it is the shape B2B deliberately deleted rather
than a second valid convention.

Each service owns its own, so this is four producer changes plus a publish and a re-pin here.

**Resolves when:** no `*Constants` type remains in a `Concertable.<Service>.Hosting` package, every
composition identity is a focused type, and this repository's `AppHost.cs` and `SystemFixture` name only
those.
