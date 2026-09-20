# Code review — Refactor/PostgresAuthComposition

> **This file is a work order, not a discussion.** If you're handed this file, fix the open `[ ]`
> findings directly and report what changed. Tick each `[x]` as you land it. Pause only for a genuinely
> irreversible or ambiguous finding: record its durable disposition, take the safe path, and keep going.

**Review status:** `complete`
**Reviewed up to commit:** `c84f00f26e822aa936f46af69a8afee682f8b702`  `(2026-09-20)`
**Judgment:** `approved`

## Review pass — 2026-09-20 — composition

**Candidate base:** `ff3453fd45ed34f5bed44a4cd6d711a67acf19be`
**Candidate head:** `c84f00f26e822aa936f46af69a8afee682f8b702`
**Candidate branch:** `Refactor/PostgresAuthComposition`
**Candidate scope:** `all`
**Candidate paths:** `Directory.Packages.props`, `compatibility/local.yaml`,
`src/Concertable.AppHost/AppHost.cs`,
`tests/shared/Concertable.AppHost.StartupTests/ResourceGraphTests.cs`,
`tests/shared/Concertable.System.E2ETests/CompositionQualificationTests.cs`,
`tests/shared/Concertable.System.E2ETests/SystemFixture.cs` `(6 paths)`
**Work-order path:** `reviews/Refactor-PostgresAuthComposition.md`
**Work-order mode:** `new`
**Pass judgment:** `approved`

Run natively against the live branch rather than through a frozen candidate bundle, so this pass records
no bundle identity or path-set digest. It was also written **after** PR #18 had already merged at
`a8066aec`, so this file is not on `main`; the follow-up PR in the ledger's Next Steps step 1 carries it.

### Findings

- [x] **R1 — MED — composition** — `tests/shared/Concertable.AppHost.StartupTests/ResourceGraphTests.cs:62`
  `ProductionGraph_IsValid` pins the provider and migration-ordering contract for B2B, Search and Payment
  — each database asserted `PostgresDatabaseResource`, each migrations resource `WaitUntilHealthy` on its
  database, each consumer `WaitForCompletion` on its migrations — and this slice added `auth-migrations`
  and moved `AuthDb` onto `postgres` without extending it. Auth is the one service whose ordering is wired
  inside the published `AddAuth` instead of at the System call site, so an Auth.Hosting publish that
  dropped `WaitForCompletion(migrations)` would compose Auth racing its own schema creation while every
  System gate stayed green. Fixed in `c84f00f2`: the three assertions added alongside the existing ones,
  and the two `"auth"` string literals in the same file replaced by `AuthConstants.Resource` now that the
  namespace is imported.

### Verified

- Both published digests re-resolved from GHCR **by the exact commit tag** `0f80b89c...`, not by recency:
  `auth` `sha256:cbd7c429...`, `auth-migrations` `sha256:090b1bb8...`. Both match `compatibility/local.yaml`.
  `Concertable.Auth.Hosting` and `Concertable.Auth.Contracts` `0.2.0-alpha.0.305` are on the feed.
- Platform `0.14` raised in both `Directory.Packages.props` and `compatibility/local.yaml`, which
  `ReleaseTrainPinTests` holds equal.
- `AddAuth` itself declares `WaitFor(authDb)` and `WaitForCompletion(migrations)`, so the absent trailing
  `WaitForCompletion` at the AppHost call site is correct and must stay absent.
- `AuthConstants.MigrationsResource` is in both rosters a run-to-completion resource needs:
  `SystemFixture.SuccessfulCompletionResources` and `CompositionQualificationTests.NonHttpServices`.
- The `sql` container stays, correctly: `customerDb` is still declared on it. Nothing else in the tree
  references the superseded auth digest, `0.2.0-alpha.0.297` or platform `0.13`.
- The Auth image's `--user root` argument, its plaintext endpoint named `https` and the
  `WithHttpsDeveloperCertificate` bridge are unchanged and still required; that constraint is already owned
  by `platform-dotnet/src/Concertable.AppHost.Shared/TECH_DEBT.md`.

### Gates

At `c84f00f2`: Release build of `Concertable.System.slnx` 0 warnings / 0 errors; startup 4/4;
architecture 4/4; `check_pins.py` green (platform `0.2.0-alpha.0.14`, 13 images composed). Remote: PR CI
`35517198677`, merge-group `35517457075`, post-merge `35517528952`, Qualify `35517580577` — all green at or
into `a8066aec`.
