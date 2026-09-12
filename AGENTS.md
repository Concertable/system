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

Per-area guidance comes from the load-on-demand skills in the `Concertable/agent-standards` plugins,
not from files here — `composition-testing`, `integration-testing`, `e2e-scenarios`, `packages`,
`microservice-boundaries` and `csharp-style` are the ones this repository actually exercises.
