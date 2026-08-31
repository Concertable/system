using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Concertable.Auth.Hosting;
using Concertable.Customer.Hosting;
using Concertable.Fleet.E2E;
using Concertable.Search.E2ETests.Helpers;

namespace Concertable.Customer.E2ETests;

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

        private void PinAuthApi(string customerApiBaseUrl)
        {
            var auth = builder.Resources
                .OfType<ProjectResource>()
                .Single(r => r.Name == AuthConstants.Resource);

            auth.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
            {
                context.EnvironmentVariables["Services__CustomerApiUrl"] = customerApiBaseUrl;
            }));
        }

        private void PinWeb(
            FleetRun run,
            IFleetProjectProvider projects)
        {
            var customerWeb = builder.Resources
                .OfType<ProjectResource>()
                .Single(r => r.Name == CustomerConstants.WebResource);

            LaunchAs(customerWeb, projects.CustomerWeb);

            customerWeb.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
            {
                context.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "E2E";
                context.EnvironmentVariables["ASPNETCORE_URLS"] = run.Profile.Endpoints.ServiceApi;
                context.EnvironmentVariables["Auth__Authority"] = run.Profile.Endpoints.Auth;
                context.EnvironmentVariables["services__payment-web__https__0"] = run.Profile.Endpoints.PaymentApi;
                context.EnvironmentVariables["ServiceAuth__ClientSecret"] = FleetRun.CustomerServiceAuthSecret;
                context.EnvironmentVariables["E2E__AdminKey"] = run.AdminKey;
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
