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
