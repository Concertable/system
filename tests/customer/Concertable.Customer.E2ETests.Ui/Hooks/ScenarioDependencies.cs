using Concertable.Customer.E2ETests.Ui.Support;
using Concertable.E2ETests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Reqnroll.Microsoft.Extensions.DependencyInjection;

namespace Concertable.Customer.E2ETests.Ui.Hooks;

public static class ScenarioDependencies
{
    [ScenarioDependencies]
    public static IServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());
        services.AddSingleton(PlaywrightHooks.Fixture);
        services.AddScoped<Browser>();
        services.AddScoped<WorkflowState>();
        services.AddScoped<IPageAccessor>(sp => sp.GetRequiredService<Browser>());
        services.AddScoped<StripeCardEntry>();
        services.AddScoped<IStripePayment, StripePayment>();
        return services;
    }
}
