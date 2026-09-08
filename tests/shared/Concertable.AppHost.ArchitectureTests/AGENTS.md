# Concertable.AppHost.ArchitectureTests — architecture tests

**The repository-wide inventory: every executable host either has a startup suite covering it or declares a
`CompositionValidationExclusion`.** It reads project files, so it asserts repository structure. Whether the
umbrella AppHost's own graph comes up is `Concertable.AppHost.StartupTests`.
