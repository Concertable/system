# Concertable.AppHost — Technical Debt

When an item is fixed, update both this file and [`ARCHITECTURE.md`](./ARCHITECTURE.md).

`Concertable.AppHost` is the **umbrella** AppHost — "I want everything wired up at once." Standalone per-service AppHosts (`Concertable.X.AppHost`) are the canonical dev experience; the umbrella should be a thin composition of per-service extension libraries, not the place where service-specific wiring lives.

## Endpoint declarations and the endpoint name belong in the Hosting packages

`AppHost.cs` declares the `"https"`-named plaintext endpoint for Auth, B2B, Customer and Search, and
hard-codes their container port. `AddPaymentWeb` already declares its own inside
`Concertable.Payment.Hosting`, which is where the other four belong: this composition should not know
any image's port, and a service is meant to arrive composed through its published
`Concertable.<Service>.Hosting` package.

B2B, Customer and Search are already fixed at the producer, adding the constant and the declaration to
each image overload with an `ImageCompositionTests` suite so the omission cannot recur. Drop the three
local declarations here once that has been published and this repository has taken the new platform
version. Auth keeps its declaration here until the same treatment reaches `AddAuth`, whose image
overload has an endpoint but had it declared by the caller rather than the package.

The endpoint name is the same problem one level down. `"https"` is retyped in `AddAuth`, `AddB2BWeb`,
`AddCustomerWeb`, `AddSearchWeb`, `AddPaymentWeb`, here, and in the black-box suite, with nothing tying
them together — which is how four of those packages came to never declare the endpoint the fifth
resolves through `GetEndpoint("https")`, leaving DCP unable to apply the Auth container's configuration
and reporting no reason for it. It is composition vocabulary like `AuthConstants.Resource` or
`PaymentConstants.GrpcPort`, so it belongs beside them in `Concertable.AppHost.Shared`. Neither
Aspire's `KnownEndpointNames` (internal) nor `Uri.UriSchemeHttps` (a URI scheme, not an endpoint name)
can stand in for it.

Both are one producer change plus a publish and a re-pin here.
