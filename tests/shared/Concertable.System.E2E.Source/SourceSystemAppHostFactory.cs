using Aspire.Hosting.Testing;
using Concertable.SystemTesting.E2E;

namespace Concertable.SystemTesting.E2E.Source;

public sealed class SourceSystemAppHostFactory : ISystemAppHostFactory
{
    public Task<IDistributedApplicationTestingBuilder> CreateBuilderAsync(
        SystemSurface surface,
        CancellationToken cancellationToken = default) =>
        surface switch
        {
            SystemSurface.B2B => DistributedApplicationTestingBuilder.CreateAsync<Projects.Concertable_B2B_AppHost>(cancellationToken),
            SystemSurface.Customer => DistributedApplicationTestingBuilder.CreateAsync<Projects.Concertable_Customer_AppHost>(cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, null),
        };

    public Aspire.Hosting.IProjectMetadata B2BWeb { get; } = new Projects.Concertable_B2B_E2ETests_Web();
    public Aspire.Hosting.IProjectMetadata CustomerWeb { get; } = new Projects.Concertable_Customer_E2ETests_Web();
    public Aspire.Hosting.IProjectMetadata PaymentWeb { get; } = new Projects.Concertable_Payment_E2ETests_Web();
    public Aspire.Hosting.IProjectMetadata PaymentWorkers { get; } = new Projects.Concertable_Payment_E2ETests_Workers();
    public Aspire.Hosting.IProjectMetadata SearchWeb { get; } = new Projects.Concertable_Search_Web();
    public Aspire.Hosting.IProjectMetadata SearchWorkers { get; } = new Projects.Concertable_Search_Workers();
}
