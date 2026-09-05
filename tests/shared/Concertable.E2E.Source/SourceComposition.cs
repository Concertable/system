using Aspire.Hosting.Testing;
using Concertable.E2E;

namespace Concertable.E2E.Source;

public sealed class SourceComposition : IComposition
{
    public Task<IDistributedApplicationTestingBuilder> CreateBuilderAsync(
        Surface surface,
        CancellationToken cancellationToken = default) =>
        surface switch
        {
            Surface.B2B => DistributedApplicationTestingBuilder.CreateAsync<Projects.Concertable_B2B_AppHost>(cancellationToken),
            Surface.Customer => DistributedApplicationTestingBuilder.CreateAsync<Projects.Concertable_Customer_AppHost>(cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, null),
        };

    public Aspire.Hosting.IProjectMetadata Auth { get; } = new Projects.Concertable_Auth();
    public Aspire.Hosting.IProjectMetadata B2BWeb { get; } = new Projects.Concertable_B2B_E2ETests_Web();
    public Aspire.Hosting.IProjectMetadata CustomerWeb { get; } = new Projects.Concertable_Customer_E2ETests_Web();
    public Aspire.Hosting.IProjectMetadata PaymentWeb { get; } = new Projects.Concertable_Payment_E2ETests_Web();
    public Aspire.Hosting.IProjectMetadata PaymentWorkers { get; } = new Projects.Concertable_Payment_E2ETests_Workers();
    public Aspire.Hosting.IProjectMetadata SearchWeb { get; } = new Projects.Concertable_Search_Web();
    public Aspire.Hosting.IProjectMetadata SearchWorkers { get; } = new Projects.Concertable_Search_Workers();
}
