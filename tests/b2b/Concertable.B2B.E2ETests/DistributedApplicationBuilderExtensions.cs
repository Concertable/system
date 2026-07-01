using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Concertable.Search.E2ETests.Helpers;
using Microsoft.Extensions.Configuration;

namespace Concertable.B2B.E2ETests;

internal static class DistributedApplicationBuilderExtensions
{
    public static IDistributedApplicationTestingBuilder AddB2BE2E(
        this IDistributedApplicationTestingBuilder builder,
        string apiBaseUrl,
        string searchApiBaseUrl,
        string authBaseUrl,
        string paymentBaseUrl)
    {
        builder.PinAuthService(authBaseUrl);
        builder.PinAuthB2BApi(apiBaseUrl);
        builder.PinB2BWeb(apiBaseUrl, authBaseUrl, paymentBaseUrl);
        builder.PinWorkers(authBaseUrl, paymentBaseUrl);
        builder.AddSearchService(searchApiBaseUrl, authBaseUrl);
        builder.PinPaymentWeb(paymentBaseUrl, authBaseUrl);
        builder.PinPaymentWorkers();
        builder.AddEphemeralSql();
        builder.PinStripeCli(paymentBaseUrl);
        return builder;
    }

    private static void PinAuthB2BApi(
        this IDistributedApplicationTestingBuilder builder,
        string apiBaseUrl)
    {
        var auth = builder.Resources
            .OfType<ProjectResource>()
            .Single(r => r.Name == AppHostConstants.ResourceNames.Auth);

        auth.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            context.EnvironmentVariables["Services__B2BApiUrl"] = apiBaseUrl;
        }));
    }

    private static void PinWorkers(
        this IDistributedApplicationTestingBuilder builder,
        string authBaseUrl,
        string paymentBaseUrl)
    {
        var workers = builder.Resources
            .OfType<ProjectResource>()
            .Single(r => r.Name == AppHostConstants.ResourceNames.Workers);

        workers.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            context.EnvironmentVariables["Auth__Authority"] = authBaseUrl;
            context.EnvironmentVariables["services__payment-web__https__0"] = paymentBaseUrl;
        }));
    }

    private static void PinB2BWeb(
        this IDistributedApplicationTestingBuilder builder,
        string apiBaseUrl,
        string authBaseUrl,
        string paymentBaseUrl)
    {
        var b2bWeb = builder.Resources
            .OfType<ProjectResource>()
            .Single(r => r.Name == AppHostConstants.ResourceNames.B2BWeb);

        var googleApiKey = builder.Configuration["GoogleApiKey"];
        var stripeSecretKey = builder.Configuration["Stripe:SecretKey"];

        b2bWeb.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            context.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "E2E";
            context.EnvironmentVariables["ASPNETCORE_URLS"] = apiBaseUrl;
            context.EnvironmentVariables["Auth__Authority"] = authBaseUrl;
            context.EnvironmentVariables["services__payment-web__https__0"] = paymentBaseUrl;
            context.EnvironmentVariables["ExternalServices__UseRealStripe"] = "true";
            context.EnvironmentVariables["ExternalServices__UseRealEmail"] = "false";
            if (!string.IsNullOrEmpty(googleApiKey))
                context.EnvironmentVariables["GoogleApiKey"] = googleApiKey;
            if (!string.IsNullOrEmpty(stripeSecretKey))
                context.EnvironmentVariables["Stripe__SecretKey"] = stripeSecretKey;
        }));
    }
}
