using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Concertable.Search.E2ETests.Helpers;

namespace Concertable.Customer.E2ETests;

internal static class DistributedApplicationBuilderExtensions
{
    public static IDistributedApplicationTestingBuilder AddCustomerE2E(
        this IDistributedApplicationTestingBuilder builder,
        string customerApiBaseUrl,
        string searchApiBaseUrl,
        string authBaseUrl,
        string paymentBaseUrl)
    {
        builder.PinAuthService(authBaseUrl);
        builder.PinAuthCustomerApi(customerApiBaseUrl);
        builder.PinCustomerWeb(customerApiBaseUrl, authBaseUrl, paymentBaseUrl);
        builder.AddSearchService(searchApiBaseUrl, authBaseUrl);
        builder.PinPaymentWeb(paymentBaseUrl, authBaseUrl);
        builder.PinPaymentWorkers();
        builder.AddEphemeralSql();
        builder.PinStripeCli(paymentBaseUrl);
        return builder;
    }

    private static void PinAuthCustomerApi(
        this IDistributedApplicationTestingBuilder builder,
        string customerApiBaseUrl)
    {
        var auth = builder.Resources
            .OfType<ProjectResource>()
            .Single(r => r.Name == AppHostConstants.ResourceNames.Auth);

        auth.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            context.EnvironmentVariables["Services__CustomerApiUrl"] = customerApiBaseUrl;
        }));
    }

    private static void PinCustomerWeb(
        this IDistributedApplicationTestingBuilder builder,
        string customerApiBaseUrl,
        string authBaseUrl,
        string paymentBaseUrl)
    {
        var customerWeb = builder.Resources
            .OfType<ProjectResource>()
            .Single(r => r.Name == AppHostConstants.ResourceNames.CustomerWeb);

        customerWeb.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            context.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "E2E";
            context.EnvironmentVariables["ASPNETCORE_URLS"] = customerApiBaseUrl;
            context.EnvironmentVariables["Auth__Authority"] = authBaseUrl;
            context.EnvironmentVariables["services__payment-web__https__0"] = paymentBaseUrl;
        }));
    }
}
