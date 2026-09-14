# Concertable.System — Technical Debt

---

## HIGH

### The local test drivers still address the monorepo tree

`scripts/test.ps1`, `scripts/unit.ps1`, `scripts/integration.ps1` and `scripts/e2e.ps1` resolve every
project under `$repoRoot/api/Concertable.<Service>/…`. That tree does not exist in this repository: the
services live in `Concertable/{auth,b2b,customer,payment,search}` and this repository keeps only
`src/`, `testkits/` and `tests/`. Each driver therefore lists no projects it can reach, whichever
command it is given.

They lost their fifth sibling in the same split. `scripts/local-platform.ps1` packed `api/Concertable.slnx`
into a local feed and is gone; the inner loop it provided now lives in the producer, as
`platform-dotnet/scripts/local-platform.ps1`, and needs no driver here. The four drivers were rewired to
call `dotnet` directly when it was removed, so nothing dangles — but nothing runs either.

The `integration-debug`, `e2e-debug`, `e2e-api-debug` and `e2e-ui-debug` skills, and
`.agents/hooks/red_run_gate.py` in `agent-standards`, all name `./scripts/<tier>.ps1` as *the* entrypoint
for a tier and expect it in the repository under test. No service repository has one. An agent following
those skills in `b2b` or `customer` today finds no entrypoint at all.

**Resolves when:** each tier has one entrypoint that a service repository actually carries, the skills
name that entrypoint, and these four drivers are either rebuilt against it or deleted.
