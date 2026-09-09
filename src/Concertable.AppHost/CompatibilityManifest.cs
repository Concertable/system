using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Concertable.AppHost;

public sealed class CompatibilityManifest
{
    public const string FileName = "local.yaml";
    public const string DirectoryName = "compatibility";

    private readonly IReadOnlyDictionary<string, ContainerImage> images;

    private CompatibilityManifest(
        string commit,
        string platformPackages,
        IReadOnlyDictionary<string, ContainerImage> images)
    {
        this.Commit = commit;
        this.PlatformPackages = platformPackages;
        this.images = images;
    }

    public string Commit { get; }

    public string PlatformPackages { get; }

    public IEnumerable<string> ServiceNames => this.images.Keys;

    public ContainerImage this[string service] =>
        this.images.TryGetValue(service, out var image)
            ? image
            : throw new InvalidOperationException(
                $"'{service}' is not pinned in {DirectoryName}/{FileName}. Pinned services: {string.Join(", ", this.images.Keys)}.");

    public static CompatibilityManifest Load(string appHostDirectory) =>
        Parse(File.ReadAllText(Locate(appHostDirectory)));

    public static CompatibilityManifest Parse(string yaml)
    {
        var document = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build()
            .Deserialize<ManifestDocument>(yaml)
            ?? throw new InvalidOperationException($"{DirectoryName}/{FileName} is empty.");

        if (document.Version != 1)
            throw new InvalidOperationException($"Unsupported {FileName} version '{document.Version}'; expected 1.");

        var commit = Required(document.Source?.Commit, "source.commit");
        var platformPackages = Required(document.Platform?.Packages, "platform.packages");

        if (document.Images is not { Count: > 0 })
            throw new InvalidOperationException($"{DirectoryName}/{FileName} pins no images.");

        var pinned = new Dictionary<string, ContainerImage>(StringComparer.Ordinal);
        foreach (var (service, image) in document.Images)
        {
            var repository = Required(image?.Repository, $"images.{service}.repository");
            var digest = Required(image?.Digest, $"images.{service}.digest");

            if (!digest.StartsWith("sha256:", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"images.{service}.digest must be a sha256 digest so the composition is immutable; got '{digest}'.");

            pinned.Add(service, new ContainerImage(repository, digest));
        }

        return new CompatibilityManifest(commit, platformPackages, pinned);
    }

    private static string Locate(string appHostDirectory)
    {
        for (var directory = new DirectoryInfo(appHostDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, DirectoryName, FileName);
            if (File.Exists(candidate))
                return candidate;
        }

        throw new InvalidOperationException(
            $"Could not locate {DirectoryName}/{FileName} walking up from '{appHostDirectory}'.");
    }

    private static string Required(string? value, string key) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{DirectoryName}/{FileName} is missing '{key}'.")
            : value;

    private sealed class ManifestDocument
    {
        public int Version { get; set; }

        public SourceSection? Source { get; set; }

        public PlatformSection? Platform { get; set; }

        public Dictionary<string, ImageSection?>? Images { get; set; }
    }

    private sealed class SourceSection
    {
        public string? Repository { get; set; }

        public string? Commit { get; set; }
    }

    private sealed class PlatformSection
    {
        public string? Packages { get; set; }
    }

    private sealed class ImageSection
    {
        public string? Repository { get; set; }

        public string? Digest { get; set; }
    }
}
