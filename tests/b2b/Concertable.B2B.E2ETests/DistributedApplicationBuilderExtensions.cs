using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Concertable.Auth.Hosting;
using Concertable.B2B.Hosting;
using Concertable.Fleet.E2E;
using Concertable.Search.E2ETests.Helpers;
using Microsoft.Extensions.Configuration;

namespace Concertable.B2B.E2ETests;

internal static class DistributedApplicationBuilderExtensions
{
    extension(IDistributedApplicationTestingBuilder builder)
    {
        public IDistributedApplicationTestingBuilder AddE2EStack(
            FleetRun run,
            IFleetProjectProvider projects,
            StripeCustomerResolver stripeCustomers)
        {
            var endpoints = run.Profile.Endpoints;
            builder.PinAuthService(endpoints.Auth, FleetRun.AuthEnvironmentVariables());
            builder.PinAuthApi(endpoints.ServiceApi);
            builder.PinWeb(run, projects);
            builder.PinWorkers(endpoints.Auth, endpoints.PaymentApi);
            builder.AddSearchService(endpoints.SearchApi, endpoints.Auth);
            builder.PinPaymentWeb(
                projects.PaymentWeb,
                endpoints.PaymentApi,
                endpoints.Auth,
                run.AdminKey,
                stripeCustomers);
            builder.PinPaymentWorkers(projects.PaymentWorkers, stripeCustomers);
            builder.AddEphemeralSql();
            builder.PinStripeCli(endpoints.PaymentApi);
            return builder;
        }

        private void PinAuthApi(string apiBaseUrl)
        {
            var auth = builder.Resources
                .OfType<ProjectResource>()
                .Single(r => r.Name == AuthConstants.Resource);

            auth.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
            {
                context.EnvironmentVariables["Services__B2BApiUrl"] = apiBaseUrl;
            }));
        }

        private void PinWorkers(
            string authBaseUrl,
            string paymentBaseUrl)
        {
            var workers = builder.Resources
                .OfType<ProjectResource>()
                .Single(r => r.Name == B2BConstants.WorkersResource);

            workers.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
            {
                context.EnvironmentVariables["Auth__Authority"] = authBaseUrl;
                context.EnvironmentVariables["services__payment-web__https__0"] = paymentBaseUrl;
                context.EnvironmentVariables["ServiceAuth__ClientSecret"] = FleetRun.B2BServiceAuthSecret;
            }));
        }

        private void PinWeb(
            FleetRun run,
            IFleetProjectProvider projects)
        {
            var b2bWeb = builder.Resources
                .OfType<ProjectResource>()
                .Single(r => r.Name == B2BConstants.WebResource);

            LaunchAs(b2bWeb, projects.B2BWeb);

            var googleApiKey = builder.Configuration["GoogleApiKey"];
            var stripeSecretKey = builder.Configuration["Stripe:SecretKey"];

            b2bWeb.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
            {
                context.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "E2E";
                context.EnvironmentVariables["ASPNETCORE_URLS"] = run.Profile.Endpoints.ServiceApi;
                context.EnvironmentVariables["Auth__Authority"] = run.Profile.Endpoints.Auth;
                context.EnvironmentVariables["services__payment-web__https__0"] = run.Profile.Endpoints.PaymentApi;
                context.EnvironmentVariables["ServiceAuth__ClientSecret"] = FleetRun.B2BServiceAuthSecret;
                context.EnvironmentVariables["E2E__AdminKey"] = run.AdminKey;
                context.EnvironmentVariables["ExternalServices__UseRealStripe"] = "true";
                context.EnvironmentVariables["ExternalServices__UseRealEmail"] = "false";
                if (!string.IsNullOrEmpty(googleApiKey))
                    context.EnvironmentVariables["GoogleApiKey"] = googleApiKey;
                if (!string.IsNullOrEmpty(stripeSecretKey))
                    context.EnvironmentVariables["Stripe__SecretKey"] = stripeSecretKey;
            }));
        }

    }

    private static void LaunchAs(ProjectResource resource, IProjectMetadata host)
    {
        foreach (var metadata in resource.Annotations.OfType<IProjectMetadata>().ToList())
            resource.Annotations.Remove(metadata);
        resource.Annotations.Add(host);
    }
}
