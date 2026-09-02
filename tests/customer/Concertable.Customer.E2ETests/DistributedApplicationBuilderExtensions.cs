using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Concertable.Auth.Hosting;
using Concertable.Customer.Hosting;
using Concertable.E2E;
using Concertable.Search.E2ETests.Helpers;

namespace Concertable.Customer.E2ETests;

internal static class DistributedApplicationBuilderExtensions
{
    extension(IDistributedApplicationTestingBuilder builder)
    {
        public IDistributedApplicationTestingBuilder AddE2EStack(
            Run run,
            IComposition composition,
            StripeCustomerResolver stripeCustomers)
        {
            var endpoints = run.Profile.Endpoints;
            builder.PinAuthService(endpoints.Auth, Run.AuthEnvironmentVariables());
            builder.PinAuthApi(endpoints.ServiceApi);
            builder.PinWeb(run, composition);
            builder.AddSearchService(
                composition.SearchWeb,
                composition.SearchWorkers,
                endpoints.SearchApi,
                endpoints.Auth);
            builder.PinPaymentWeb(
                composition.PaymentWeb,
                endpoints.PaymentApi,
                endpoints.Auth,
                run.AdminKey,
                stripeCustomers);
            builder.PinPaymentWorkers(composition.PaymentWorkers, stripeCustomers);
            builder.AddEphemeralSql();
            builder.PinStripeCli(endpoints.PaymentApi);
            return builder;
        }

        private void PinAuthApi(string customerApiBaseUrl)
        {
            var auth = builder.Resources
                .Single(r => r.Name == AuthConstants.Resource);

            auth.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
            {
                context.EnvironmentVariables["Services__CustomerApiUrl"] = customerApiBaseUrl;
            }));
        }

        private void PinWeb(
            Run run,
            IComposition composition)
        {
            var customerWeb = builder.Resources
                .OfType<ProjectResource>()
                .Single(r => r.Name == CustomerConstants.WebResource);

            ReplaceProjectMetadata(customerWeb, composition.CustomerWeb);

            customerWeb.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
            {
                context.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "E2E";
                context.EnvironmentVariables["ASPNETCORE_URLS"] = run.Profile.Endpoints.ServiceApi;
                context.EnvironmentVariables["Auth__Authority"] = run.Profile.Endpoints.Auth;
                context.EnvironmentVariables["services__payment-web__https__0"] = run.Profile.Endpoints.PaymentApi;
                context.EnvironmentVariables["ServiceAuth__ClientSecret"] = Run.CustomerServiceAuthSecret;
                context.EnvironmentVariables["E2E__AdminKey"] = run.AdminKey;
            }));
        }

    }

    private static void ReplaceProjectMetadata(ProjectResource resource, IProjectMetadata host)
    {
        foreach (var metadata in resource.Annotations.OfType<IProjectMetadata>().ToList())
            resource.Annotations.Remove(metadata);
        resource.Annotations.Add(host);
    }
}
