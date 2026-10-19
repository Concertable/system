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

<!-- BEGIN personal:current-solution sha256:2e091b5f18f67fb2302024e817c71d4a0f5234c1ca63ca1c9af5c4eef3b6a128 -->
# Personal projects

These rules are mandatory in projects that explicitly adopt `personal@tj-agents`. Adoption records the
project's personal scope; a home directory, author name or employer association never establishes it.
Employer projects remain outside this profile unless Tommy explicitly changes that scope.

Keep one current supported solution. When replacing behavior, remove the superseded names, aliases,
fallback implementations, adapters, configuration formats and compatibility branches. Coordinate and
migrate all known callers, project declarations, installed registrations, generated artifacts and live
runtime entry points in the same cutover. Do not retain obsolete behavior for hypothetical external users;
coordinate upgrades with the actual collaborating developers.

Verify the entry points people actually use on every supported platform, including Windows and Linux
launch paths where supported. Source edits, successful generation, passing unit tests and version commands
alone do not prove that a migration is complete. Check installed bindings and fresh runtime behavior.
Record any unverified entry point as incomplete, with its resolver and observable acceptance condition.

For example, replacing `old-launch` with `launch` means migrating the registered shell command, project
configuration and actual callers, deleting the old entry, then invoking `launch` from a fresh session on
each supported OS. Running `launch --version` alone leaves the cutover unverified.

```text
Replace old-launch with launch:
1. Migrate the registered shell command, configuration and actual callers.
2. Delete the old entry and regenerate installed settings.
3. Invoke launch from a fresh supported Windows/Linux session and check its actual behavior.
Result: launch --version alone leaves this migration incomplete.
```

Do not independently add a backwards-compatibility exception. Surface an explicit contrary user requirement
as a deliberate policy decision, identify the affected supported behavior and reconcile this profile's
scope before implementing it. Never hide an exception in a fallback or adapter.

Solve the demonstrated problem with the smallest suitable implementation. Add abstractions, dependencies,
configuration switches, duplicate paths or process machinery only for a concrete current need. Prefer the
capabilities the project already selects. Verify the behavior and meaningful failure paths in proportion
to the change; do not add tests that merely repeat the implementation or a checklist with no concrete benefit.

This policy is authored once here. Personal adoption generates its marked native instruction block from
this body and checks it for drift. A selected plugin or an `always` tier alone is not proof of loading.
<!-- END personal:current-solution -->
