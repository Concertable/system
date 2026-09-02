using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Concertable.Auth.Hosting;
using Concertable.Payment.Hosting;
using Microsoft.Extensions.Configuration;

namespace Concertable.Testing.E2E;

public static class DistributedApplicationBuilderExtensions
{
    extension(IDistributedApplicationTestingBuilder builder)
    {
        internal void PinPaymentWeb(
            IProjectMetadata project,
            string paymentApiEndpoint,
            string authEndpoint,
            string adminKey,
            StripeCustomerResolver stripeCustomers)
        {
            var paymentWeb = builder.GetRequiredResource(PaymentConstants.WebResource);

            paymentWeb = SubstituteE2EProject(builder, paymentWeb, project);
            PinHttpsEndpoint(builder, paymentWeb, new Uri(paymentApiEndpoint).Port);

            var stripeSecretKey = builder.Configuration["Stripe:SecretKey"];

            paymentWeb.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
            {
                context.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "E2E";
                context.EnvironmentVariables["ASPNETCORE_URLS"] = paymentApiEndpoint;
                context.EnvironmentVariables["Auth__Authority"] = authEndpoint;
                context.EnvironmentVariables["E2E__AdminKey"] = adminKey;
                AddStripeCustomerConfiguration(context, stripeCustomers);
                if (!string.IsNullOrEmpty(stripeSecretKey))
                    context.EnvironmentVariables["Stripe__SecretKey"] = stripeSecretKey;
            }));
        }

        internal void PinAuthService(
            string authEndpoint,
            IReadOnlyDictionary<string, string> environmentVariables)
        {
            var auth = builder.GetRequiredResource(AuthConstants.Resource);

            auth.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
            {
                context.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "E2E";
                context.EnvironmentVariables["ASPNETCORE_URLS"] = authEndpoint;
                context.EnvironmentVariables["Auth__Authority"] = authEndpoint;
                foreach (var (key, value) in environmentVariables)
                    context.EnvironmentVariables[key] = value;
            }));
        }

        internal void PinPaymentWorkers(
            IProjectMetadata project,
            StripeCustomerResolver stripeCustomers)
        {
            var paymentWorkers = builder.GetRequiredResource(PaymentConstants.WorkersResource);

            paymentWorkers = SubstituteE2EProject(builder, paymentWorkers, project);

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

        internal void PinStripeCli(string paymentApiEndpoint)
        {
            var stripeCli = builder.Resources
                .SingleOrDefault(r => r.Name == PaymentConstants.StripeCliResource);

            if (stripeCli is null) return;

            var apiKey = builder.Configuration["Stripe:SecretKey"]
                ?? throw new InvalidOperationException("Stripe:SecretKey is not configured.");
            var forwardTo = $"{paymentApiEndpoint}/api/Webhook";

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

        internal void AddEphemeralSql()
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

    internal static IResource GetRequiredResource(
        this IDistributedApplicationBuilder builder,
        string name) =>
        builder.Resources.Single(resource => resource.Name == name);

    internal static IResource SubstituteE2EProject(
        IDistributedApplicationBuilder builder,
        IResource resource,
        IProjectMetadata host)
    {
        if (resource is ProjectResource)
        {
            foreach (var metadata in resource.Annotations.OfType<IProjectMetadata>().ToList())
                resource.Annotations.Remove(metadata);
            resource.Annotations.Add(host);
            return resource;
        }

        if (resource is not ContainerResource)
            throw new InvalidOperationException(
                $"E2E host pinning does not support resource '{resource.Name}' of type '{resource.GetType().Name}'.");

        // A production Payment image intentionally has no TestKit routes or Stripe adapter. Keep
        // the foreign image in the imported graph, but do not start it; run the Payment-owned E2E
        // project beside it and retarget waits to that host. This preserves the service boundary
        // while making image-backed umbrella AppHosts exercise the same E2E behavior as source ones.
        builder.CreateResourceBuilder(resource).WithExplicitStart();
        foreach (var endpoint in resource.Annotations
                     .OfType<EndpointAnnotation>()
                     .Where(endpoint => endpoint.Name == "https" && endpoint.Port is not null))
            endpoint.Port = null;

        var e2eProject = builder.AddResource(new ProjectResource($"{resource.Name}-e2e"))
            .WithAnnotation(host)
            .Resource;

        foreach (var annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
            e2eProject.Annotations.Add(annotation);
        foreach (var annotation in resource.Annotations.OfType<WaitAnnotation>())
            e2eProject.Annotations.Add(annotation);

        var e2eProjectBuilder = builder.CreateResourceBuilder((IResource)e2eProject);
        foreach (var dependent in builder.Resources.Where(candidate => !ReferenceEquals(candidate, resource)))
        {
            var waits = dependent.Annotations
                .OfType<WaitAnnotation>()
                .Where(annotation => ReferenceEquals(annotation.Resource, resource))
                .ToList();
            if (waits.Count == 0)
                continue;

            foreach (var wait in waits)
                dependent.Annotations.Remove(wait);
            builder.CreateResourceBuilder((IResourceWithWaitSupport)dependent).WaitFor(e2eProjectBuilder);
        }

        return e2eProject;
    }

    internal static void PinHttpsEndpoint(
        IDistributedApplicationBuilder builder,
        IResource resource,
        int port)
    {
        foreach (var endpoint in resource.Annotations
                     .OfType<EndpointAnnotation>()
                     .Where(endpoint => endpoint.Name == "https")
                     .ToList())
            resource.Annotations.Remove(endpoint);

        builder.CreateResourceBuilder((IResourceWithEndpoints)resource)
            .WithHttpsEndpoint(port: port, isProxied: false);
    }
}
