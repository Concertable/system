using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace Concertable.E2E;

public interface IComposition
{
    Task<IDistributedApplicationTestingBuilder> CreateBuilderAsync(
        Surface surface,
        CancellationToken cancellationToken = default);

    IProjectMetadata Auth { get; }
    IProjectMetadata B2BWeb { get; }
    IProjectMetadata CustomerWeb { get; }
    IProjectMetadata PaymentWeb { get; }
    IProjectMetadata PaymentWorkers { get; }
    IProjectMetadata SearchWeb { get; }
    IProjectMetadata SearchWorkers { get; }
}
