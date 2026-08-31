using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace Concertable.SystemTesting.E2E;

public interface ISystemAppHostFactory
{
    Task<IDistributedApplicationTestingBuilder> CreateBuilderAsync(
        SystemSurface surface,
        CancellationToken cancellationToken = default);

    IProjectMetadata B2BWeb { get; }
    IProjectMetadata CustomerWeb { get; }
    IProjectMetadata PaymentWeb { get; }
    IProjectMetadata PaymentWorkers { get; }
    IProjectMetadata SearchWeb { get; }
    IProjectMetadata SearchWorkers { get; }
}
