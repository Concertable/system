using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Concertable.Auth.Hosting;
using Concertable.Customer.Hosting;
using Concertable.Search.E2ETests.Helpers;

namespace Concertable.Customer.E2ETests;

internal static class DistributedApplicationBuilderExtensions
{
    public static IDistributedApplicationTestingBuilder AddE2EStack(
        this IDistributedApplicationTestingBuilder builder,
        string customerApiBaseUrl,
        string searchApiBaseUrl,
        string authBaseUrl,
        string paymentBaseUrl,
        StripeCustomerResolver stripeCustomers)
    {
        builder.PinAuthService(authBaseUrl);
        builder.PinAuthApi(customerApiBaseUrl);
        builder.PinWeb(customerApiBaseUrl, authBaseUrl, paymentBaseUrl);
        builder.AddSearchService(searchApiBaseUrl, authBaseUrl);
        builder.PinPaymentWeb(paymentBaseUrl, authBaseUrl, stripeCustomers);
        builder.PinPaymentWorkers(stripeCustomers);
        builder.AddEphemeralSql();
        builder.PinStripeCli(paymentBaseUrl);
        return builder;
    }

    private static void PinAuthApi(
        this IDistributedApplicationTestingBuilder builder,
        string customerApiBaseUrl)
    {
        var auth = builder.Resources
            .OfType<ProjectResource>()
            .Single(r => r.Name == AuthConstants.Resource);

        auth.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            context.EnvironmentVariables["Services__CustomerApiUrl"] = customerApiBaseUrl;
        }));
    }

    private static void PinWeb(
        this IDistributedApplicationTestingBuilder builder,
        string customerApiBaseUrl,
        string authBaseUrl,
        string paymentBaseUrl)
    {
        var customerWeb = builder.Resources
            .OfType<ProjectResource>()
            .Single(r => r.Name == CustomerConstants.WebResource);

        customerWeb.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            context.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "E2E";
            context.EnvironmentVariables["ASPNETCORE_URLS"] = customerApiBaseUrl;
            context.EnvironmentVariables["Auth__Authority"] = authBaseUrl;
            context.EnvironmentVariables["services__payment-web__https__0"] = paymentBaseUrl;
            context.EnvironmentVariables["ServiceAuth__ClientSecret"] = Concertable.Testing.E2E.DistributedApplicationBuilderExtensions.CustomerServiceAuthSecret;
        }));
    }
}
