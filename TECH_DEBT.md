# Tech debt

## MED

### Renovate cannot read the image pins in `compatibility/local.yaml`

The header of that file says Renovate raises the digest bumps here. It cannot. Each image is stored as a
`repository`/`digest` pair with no tag:

```yaml
  auth:
    repository: ghcr.io/concertable/auth
    digest: sha256:d888cb…
```

A digest names an artifact but not a position in a sequence, so nothing can decide which digest is
*newer*. `pipeline/docs/MANIFEST_CONTRACTS.md` already defines the shape that can be ordered —
`ghcr.io/concertable/<image>:<immutable-semver>@sha256:<digest>` — and the organization Renovate preset
already extracts it. This file is the only image-pin owner in the estate and the only one not written
that way, so every digest here is raised by hand.

The package trains in the same file (`platform.packages`, `services.*`) are read by the preset and are
not affected.

Blocked, not merely unstarted: three of the twelve images — `payment-web`, `payment-workers` and
`payment-migrations` — carry no semver tag at all, only `latest` and a commit SHA, so a conforming
reference cannot be written for them today. That half is recorded in `payment`'s own `TECH_DEBT.md`.

**Resolves when:** every image this file pins has an immutable semver tag, each entry is a single
canonical reference, `CompatibilityManifest` parses that reference, and a Renovate run raises one of
them.

### The manifest restates package trains that `Directory.Packages.props` already owns

`compatibility/local.yaml` declares `platform.packages` and a `services.<name>` version per service.
`Directory.Packages.props` declares the same versions as release-train properties, and
`ReleaseTrainPinTests` exists solely to hold the two equal. One fact, two declarations, plus a test
whose only job is policing the duplication.

The duplicated half is also dead. `CompatibilityManifest` parses those values into `PlatformPackages`
and `ServicePackages`, and nothing reads either — not the AppHost, not the E2E compositions, nothing.
Only `images` is load-bearing, and it is parsed separately.

So the block is deletable outright: the `platform` and `services` keys, the two properties and the
constructor parameters behind them, and `ReleaseTrainPinTests`. `Directory.Packages.props` is then the
single owner of every package version, and this file pins only what it alone knows — the images.

That deletion is also what lets the estate split dependency updates by file format, with Dependabot
owning `Directory.Packages.props` and Renovate retained only for this file. `pipeline`'s `TECH_DEBT.md`
carries that half.

One property must be replaced rather than dropped. Today a single bot raises a service's package pin and
its image digest in one pull request, so they cannot diverge. Under two bots they land independently,
and nothing would catch a composition whose Payment contracts are `0.2.0-alpha.0.400` while its Payment
images were built from `0.2.0-alpha.0.372`. `ReleaseTrainPinTests` never covered that — it compares
packages to packages — so this is a gap that exists now and merely becomes more reachable.

**Resolves when:** `platform` and `services` are gone from this file, the dead properties and
`ReleaseTrainPinTests` are deleted, and a test holds each image's tag equal to its service's release-train
property in `Directory.Packages.props`.
