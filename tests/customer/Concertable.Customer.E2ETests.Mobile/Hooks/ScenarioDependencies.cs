using Concertable.Customer.E2ETests.Mobile.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Reqnroll.Microsoft.Extensions.DependencyInjection;

namespace Concertable.Customer.E2ETests.Mobile.Hooks;

public static class ScenarioDependencies
{
    [ScenarioDependencies]
    public static IServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());
        services.AddSingleton(_ => EmulatorHooks.Fixture);
        services.AddScoped<MobileApp>();
        services.AddScoped<WorkflowState>();
        services.AddScoped<StripePaymentSheet>();
        return services;
    }
}
