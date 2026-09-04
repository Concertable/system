# Concertable.AppHost.StartupTests — startup tests

**Would the umbrella AppHost refuse to come up?** Builds the umbrella resource graph without starting any
resource. The repository-wide inventory that every executable host is covered or explicitly excluded stays
in `Concertable.AppHost.ArchitectureTests`, because it asserts project structure rather than startup.
