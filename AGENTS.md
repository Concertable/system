# Concertable System

Composes the Concertable fleet from published container images and qualifies it as a black box.
Start at [`README.md`](./README.md) for the layout and how to run it.

**This repository owns no service source, and no path here may reintroduce any.** A service arrives
only as an image pinned by digest in `compatibility/local.yaml`, composed through a published
`Concertable.<Service>.Hosting` package. `ResourceGraphTests` fails the build if the app model gains
a project or node resource, and `.github/scripts/check_pins.py` fails if the manifest, the platform
pin and the services the AppHost composes disagree. Adding a `ProjectReference` to a service, or a
second place that names an image, is the thing both gates exist to stop.

**Never weaken the Docker pre-flight.** `scripts/docker-health.ps1` exists because `docker ps`
answering is not proof Docker is healthy, and the suites otherwise fail minutes later at SQL startup
with zero scenarios run. Its own header says why each weaker check was rejected.

## Standards

Per-area guidance comes from the load-on-demand skills in the `Concertable/agents` plugin,
not from files here. Load `concertable:composition-testing`, `concertable:integration-testing`,
`dotnet:testing-e2e`, `concertable:packages`, `concertable:microservice-boundaries` and
`dotnet:style` for the areas this repository exercises.

<!-- BEGIN personal:rules sha256:eee7a719bb90e67aaa81ac101a45836dd099bc745fee61216c99ec7d6075f12d -->
# Personal rules

These rules govern every project that explicitly adopts `personal@tj-agents`. Adoption records the
project's personal scope; a home directory, author name or employer association never establishes it, and
employer projects stay outside this profile unless the user explicitly extends that scope. Before planning
or starting work, confirm the task is clear against each rule below.

## 1. Clean cut

Keep one current supported solution. When replacing behavior, remove the superseded names, aliases,
fallback implementations, adapters, configuration formats, compatibility branches and forwarding or
migration docs. Migrate all known callers, project declarations, installed registrations, generated
artifacts and live runtime entry points in the same cutover, and coordinate upgrades with the actual
collaborating developers. Run each migration once during the cutover and delete it; never ship migration
or cleanup code that runs again later. CI fails a commit that removes or renames a skill while a tracked source
still names its old `<package>:<name>`.

Verify the entry points people actually use on every supported platform, including Windows and Linux
launch paths where supported. Source edits, successful generation, passing unit tests and version commands
alone leave a migration unproven; check installed bindings and fresh runtime behavior. Record any
unverified entry point as incomplete, with its resolver and observable acceptance condition.

```text
Replace old-launch with launch:
1. Migrate the registered shell command, configuration and actual callers.
2. Delete the old entry and regenerate installed settings.
3. Invoke launch from a fresh supported Windows/Linux session and check its actual behavior.
Result: launch --version alone leaves this migration incomplete.
```

A backwards-compatibility exception exists only as an explicit user requirement. Surface it as a
deliberate policy decision, identify the affected supported behavior and reconcile this profile's scope
before implementing it, keeping the exception visible rather than inside a fallback or adapter.

## 2. Proportionate implementation

Solve the demonstrated problem with the smallest suitable implementation. Add abstractions, dependencies,
configuration switches, duplicate paths or process machinery only for a concrete current need. Prefer the
capabilities the project already selects. Verify the behavior and meaningful failure paths in proportion
to the change, with tests that prove behavior rather than restate the implementation.

## 3. Plans

Track work as a GitHub issue or project item. A plan committed under `plans/` is only the detailed
implementation plan behind one such item and links to it; working plans stay machine-local.

This policy is authored once here. A selecting project receives it at SessionStart, and adoption generates
its marked native instruction block from this body and checks it for drift. A selected plugin or an
`always` tier alone is not proof of loading.
<!-- END personal:rules -->
