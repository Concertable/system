# Concertable System

Composes the Concertable fleet from **published container images** and qualifies it as a black box.

`System` names this repository's responsibility — composing and shipping the running system — not a
.NET namespace. No code artifact here sits under a `System` root.

This repository owns no service source. Every service arrives as an image pinned by digest in
[`compatibility/local.yaml`](./compatibility/local.yaml), and every composition extension arrives as
a published `Concertable.<Service>.Hosting` package. There is deliberately no path that runs a
service from source: a foreign service compiled from source is what this repository exists to stop.

## Layout

| Path | Holds |
|---|---|
| `src/Concertable.AppHost/` | the umbrella Aspire AppHost — container-only composition |
| `compatibility/local.yaml` | the pinned image digests and platform package version |
| `testkits/Concertable.Testing.E2E/` | the shared E2E harness |
| `tests/shared/` | cross-surface E2E contracts, AppHost architecture and startup tests |
| `tests/{b2b,customer,payment,search}/` | per-surface qualification suites |
| `scripts/` | local entrypoints, including the Docker pre-flight gate |

## Running it

The composition restores from the org feed and pulls from GHCR, so both need credentials:

```sh
export GITHUB_PACKAGES_TOKEN=<PAT with read:packages>
docker login ghcr.io
```

Then:

```sh
dotnet build Concertable.System.slnx
dotnet run --project src/Concertable.AppHost
```

**Always run the Docker pre-flight before any E2E run.** A half-started Docker engine keeps
answering `docker ps` while port forwarding for new containers is dead, and the suites only discover
it minutes later at SQL startup with zero scenarios executed:

```sh
powershell.exe -NoProfile -File ./scripts/docker-health.ps1
```

Exit 0 means safe to proceed; exit 1 means fix Docker first rather than starting a suite.

## Bumping the composition

`compatibility/local.yaml` is the only place an image pin is edited — the AppHost reads it and fails
composition if a service is unpinned or its digest is not a `sha256:` digest, so no second list can
drift from what actually runs. Keep `platform.packages` equal to `ConcertableDotNetPlatformVersion`
in `src/Concertable.AppHost/Directory.Packages.props`.

## Where this came from

Extracted from the `Concertable/concertable` monorepo with `git filter-repo` 2.47.0 against the path
map in that repo's `eng/repository-split/map.yaml`, preserving the full history of every path it
claims. The images and packages currently pinned were produced by that monorepo; each service takes
over publishing its own as it is promoted to its own repository.
