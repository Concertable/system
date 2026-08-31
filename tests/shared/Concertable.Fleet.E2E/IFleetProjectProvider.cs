using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace Concertable.Fleet.E2E;

public interface IFleetProjectProvider
{
    Task<IDistributedApplicationTestingBuilder> CreateBuilderAsync(
        FleetSurface surface,
        CancellationToken cancellationToken = default);

    IProjectMetadata B2BWeb { get; }
    IProjectMetadata CustomerWeb { get; }
    IProjectMetadata PaymentWeb { get; }
    IProjectMetadata PaymentWorkers { get; }
    IProjectMetadata SearchWeb { get; }
    IProjectMetadata SearchWorkers { get; }
}
