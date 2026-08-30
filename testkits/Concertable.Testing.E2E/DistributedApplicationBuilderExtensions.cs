using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Concertable.Auth.Hosting;
using Concertable.Payment.Hosting;
using Microsoft.Extensions.Configuration;

namespace Concertable.Testing.E2E;

public static class DistributedApplicationBuilderExtensions
{
    private const string AuthServiceAuthSecret = "concertable-e2e-auth-service-secret";

    internal static void PinPaymentWeb(
        this IDistributedApplicationTestingBuilder builder,
        FleetRun run,
        IFleetProjectProvider projects,
        StripeCustomerResolver stripeCustomers)
    {
        var paymentWeb = builder.Resources
            .OfType<ProjectResource>()
            .Single(r => r.Name == PaymentConstants.WebResource);

        paymentWeb.LaunchAs(projects.PaymentWeb);

        var stripeSecretKey = builder.Configuration["Stripe:SecretKey"];

        paymentWeb.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            context.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "E2E";
            context.EnvironmentVariables["ASPNETCORE_URLS"] = run.Profile.Endpoints.PaymentApi;
            context.EnvironmentVariables["Auth__Authority"] = run.Profile.Endpoints.Auth;
            context.EnvironmentVariables["E2E__AdminKey"] = run.AdminKey;
            AddStripeCustomerConfiguration(context, stripeCustomers);
            if (!string.IsNullOrEmpty(stripeSecretKey))
                context.EnvironmentVariables["Stripe__SecretKey"] = stripeSecretKey;
        }));
    }

    internal static void PinAuthService(
        this IDistributedApplicationTestingBuilder builder,
        FleetRun run)
    {
        var auth = builder.Resources
            .OfType<ProjectResource>()
            .Single(r => r.Name == AuthConstants.Resource);

        auth.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            context.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "E2E";
            context.EnvironmentVariables["ASPNETCORE_URLS"] = run.Profile.Endpoints.Auth;
            context.EnvironmentVariables["Auth__Authority"] = run.Profile.Endpoints.Auth;
            context.EnvironmentVariables["ServiceAuth__B2BClientSecret"] = FleetRun.B2BServiceAuthSecret;
            context.EnvironmentVariables["ServiceAuth__CustomerClientSecret"] = FleetRun.CustomerServiceAuthSecret;
            context.EnvironmentVariables["ServiceAuth__AuthClientSecret"] = AuthServiceAuthSecret;
        }));
    }

    internal static void PinPaymentWorkers(
        this IDistributedApplicationTestingBuilder builder,
        IFleetProjectProvider projects,
        StripeCustomerResolver stripeCustomers)
    {
        var paymentWorkers = builder.Resources
            .OfType<ProjectResource>()
            .Single(r => r.Name == PaymentConstants.WorkersResource);

        paymentWorkers.LaunchAs(projects.PaymentWorkers);

        paymentWorkers.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            context.EnvironmentVariables["DOTNET_ENVIRONMENT"] = "E2E";
            AddStripeCustomerConfiguration(context, stripeCustomers);
        }));
    }

    private static void AddStripeCustomerConfiguration(
        EnvironmentCallbackContext context,
        StripeCustomerResolver stripeCustomers)
    {
        foreach (var (key, value) in stripeCustomers.GetConfiguration())
            context.EnvironmentVariables[key.Replace(":", "__")] = value;
    }

    private static void LaunchAs(this ProjectResource resource, IProjectMetadata host)
    {
        foreach (var metadata in resource.Annotations.OfType<IProjectMetadata>().ToList())
            resource.Annotations.Remove(metadata);
        resource.Annotations.Add(host);
    }

    internal static void PinStripeCli(
        this IDistributedApplicationTestingBuilder builder,
        FleetRun run)
    {
        var stripeCli = builder.Resources
            .SingleOrDefault(r => r.Name == PaymentConstants.StripeCliResource);

        if (stripeCli is null) return;

        var apiKey = builder.Configuration["Stripe:SecretKey"]
            ?? throw new InvalidOperationException("Stripe:SecretKey is not configured.");
        var forwardTo = $"{run.Profile.Endpoints.PaymentApi}/api/Webhook";

        foreach (var annotation in stripeCli.Annotations.OfType<CommandLineArgsCallbackAnnotation>().ToList())
            stripeCli.Annotations.Remove(annotation);

        stripeCli.Annotations.Add(new CommandLineArgsCallbackAnnotation(ctx =>
        {
            ctx.Args.Add("listen");
            ctx.Args.Add("--skip-verify");
            ctx.Args.Add("--api-key");
            ctx.Args.Add(apiKey);
            ctx.Args.Add("--forward-to");
            ctx.Args.Add(forwardTo);
            return Task.CompletedTask;
        }));
    }

    internal static void AddEphemeralSql(
        this IDistributedApplicationTestingBuilder builder)
    {
        var sql = builder.Resources
            .OfType<SqlServerServerResource>()
            .Single();

        var volume = sql.Annotations
            .OfType<ContainerMountAnnotation>()
            .FirstOrDefault();

        if (volume is not null)
            sql.Annotations.Remove(volume);
    }
}
