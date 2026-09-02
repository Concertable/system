# Concertable.AppHost.ArchitectureTests — architecture tests

**Architecture fitness functions for the composed AppHost graph. The dynamic host-graph checks build the
real production registration graph without starting it or external infrastructure, and the inventory guard
asserts every executable host declares coverage or an explicit exclusion. Tests that execute requests,
business operations or infrastructure belong in integration or E2E projects.**

Host coverage and activation rules: the `composition-testing` skill.
