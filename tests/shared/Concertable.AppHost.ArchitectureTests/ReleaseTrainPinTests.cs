using System.Xml.Linq;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Concertable.AppHost.ArchitectureTests;

public sealed class ReleaseTrainPinTests
{
    // The restored pin and the qualified manifest are two declarations of one fact, and until this
    // suite existed only a comment in Directory.Packages.props held them equal. They drifted: the
    // manifest sat at 0.1.0-alpha.0.1364 while the pins moved on, so the "known-good composition"
    // named packages the repository did not build against and nothing failed.
    private const string PlatformProperty = "ConcertableDotNetPlatformVersion";

    private static readonly Dictionary<string, string> TrainProperties = new(StringComparer.Ordinal)
    {
        ["auth"] = "ConcertableAuthVersion",
        ["b2b"] = "ConcertableB2BContractsVersion",
        ["customer"] = "ConcertableCustomerVersion",
        ["payment"] = "ConcertablePaymentVersion",
        ["search"] = "ConcertableSearchVersion",
    };

    [Fact]
    public void PlatformTrain_MatchesTheRestoredPin()
    {
        var pins = RestoredPins();
        Assert.True(pins.ContainsKey(PlatformProperty), $"Directory.Packages.props declares no <{PlatformProperty}>.");
        Assert.Equal(pins[PlatformProperty], QualifiedManifest().Platform?.Packages);
    }

    [Fact]
    public void EveryQualifiedServiceTrain_MatchesItsRestoredPin()
    {
        var pins = RestoredPins();
        var services = QualifiedManifest().Services;

        Assert.NotNull(services);
        Assert.NotEmpty(services);

        foreach (var (service, version) in services)
        {
            Assert.True(
                TrainProperties.TryGetValue(service, out var property),
                $"services.{service} names no known train. Known: {string.Join(", ", TrainProperties.Keys)}.");
            Assert.True(
                pins.ContainsKey(property),
                $"Directory.Packages.props declares no <{property}> for services.{service}.");
            Assert.Equal(pins[property], version);
        }
    }

    // A train added to the build without a manifest entry restores fine and then qualifies against a
    // version the manifest never names, which is the drift this pair of files exists to prevent.
    [Fact]
    public void EveryRestoredTrain_IsNamedByTheManifest()
    {
        var named = (QualifiedManifest().Services ?? [])
            .Keys
            .Select(service => TrainProperties.TryGetValue(service, out var property) ? property : service)
            .Append(PlatformProperty)
            .ToHashSet(StringComparer.Ordinal);

        var unnamed = RestoredPins().Keys
            .Where(property => !named.Contains(property))
            .OrderBy(property => property, StringComparer.Ordinal)
            .ToList();

        Assert.Empty(unnamed);
    }

    private static Dictionary<string, string> RestoredPins() =>
        XDocument.Load(Path.Combine(RepositoryRoot(), "Directory.Packages.props"))
            .Descendants("PropertyGroup")
            .Elements()
            .Where(element => element.Name.LocalName.StartsWith("Concertable", StringComparison.Ordinal)
                && element.Name.LocalName.EndsWith("Version", StringComparison.Ordinal))
            .ToDictionary(
                element => element.Name.LocalName,
                element => element.Value.Trim(),
                StringComparer.Ordinal);

    private static ManifestShape QualifiedManifest() =>
        new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build()
            .Deserialize<ManifestShape>(
                File.ReadAllText(Path.Combine(RepositoryRoot(), "compatibility", "local.yaml")));

    // Mirrors InventoryTests: the shared helper only recognises a root containing `api/`, which is the
    // monorepo's shape and not this one's.
    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Concertable.System.slnx")))
                return directory.FullName;
        }

        throw new InvalidOperationException(
            $"Could not locate the repository root walking up from '{AppContext.BaseDirectory}'.");
    }

    private sealed class ManifestShape
    {
        public PlatformShape? Platform { get; set; }

        public Dictionary<string, string>? Services { get; set; }
    }

    private sealed class PlatformShape
    {
        public string? Packages { get; set; }
    }
}
