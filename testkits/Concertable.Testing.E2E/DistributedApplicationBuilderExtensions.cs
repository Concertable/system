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

        internal IResource PinAuthService(
            IProjectMetadata project,
            string authEndpoint,
            IReadOnlyDictionary<string, string> environmentVariables)
        {
            var auth = builder.GetRequiredResource(AuthConstants.Resource);

            // The pinned image binds HTTP only and cannot be handed a certificate — Aspire's
            // developer-certificate annotation never reaches it, so forcing ASPNETCORE_URLS onto https
            // made Kestrel fail to bind ("No server certificate was specified") and leaving it alone
            // served plaintext on the port declared as https ("Cannot determine the frame size").
            // Consumers set RequireHttpsMetadata outside Development, so E2E needs a real TLS Auth:
            // run it from source like every other source-backed host here, which is also what the
            // Payment hosts do with their own production images.
            auth = SubstituteE2EProject(builder, auth, project);
            PinHttpsEndpoint(builder, auth, new Uri(authEndpoint).Port);

            auth.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
            {
                context.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "E2E";
                context.EnvironmentVariables["ASPNETCORE_URLS"] = authEndpoint;
                context.EnvironmentVariables["Auth__Authority"] = authEndpoint;
                foreach (var (key, value) in environmentVariables)
                    context.EnvironmentVariables[key] = value;
            }));

            return auth;
        }

        internal void PinPaymentWorkers(
            IProjectMetadata project,
            StripeCustomerResolver stripeCustomers)
        {
            var paymentWorkers = builder.GetRequiredResource(PaymentConstants.WorkersResource);

            paymentWorkers = SubstituteE2EProject(builder, paymentWorkers, project);

            // SubstituteE2EProject carries the container's reference wiring but not its static
            // WithEnvironment / AddSecrets values, so re-set the ones the Payment workers host requires
            // at startup (matches AddSearchService's own image path).
            var stripeSecretKey = builder.Configuration["Stripe:SecretKey"];

            paymentWorkers.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
            {
                context.EnvironmentVariables["DOTNET_ENVIRONMENT"] = "E2E";
                context.EnvironmentVariables["ServiceBus__ServiceName"] = PaymentConstants.ServiceName;
                if (!string.IsNullOrEmpty(stripeSecretKey))
                    context.EnvironmentVariables["Stripe__SecretKey"] = stripeSecretKey;
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

    extension(IDistributedApplicationBuilder builder)
    {
        internal IResource GetRequiredResource(string name) =>
            builder.Resources.Single(resource => resource.Name == name);
    }

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

        var e2eProjectBuilder = builder.AddResource(new ProjectResource($"{resource.Name}-e2e"))
            .WithAnnotation(host);
        var e2eProject = e2eProjectBuilder.Resource;

        foreach (var annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
            e2eProject.Annotations.Add(annotation);
        foreach (var annotation in resource.Annotations.OfType<WaitAnnotation>())
            e2eProject.Annotations.Add(annotation);

        // The container's WithReference(...) calls injected connection strings through callbacks bound to
        // the original resource, which Aspire does not resolve for the substituted project — payment
        // workers then start with a null 'asb' connection string and crash. Re-issue the reference for
        // every connection-string resource the container waited on (this is what AddSearchService does
        // on its own image path).
        foreach (var connectionResource in resource.Annotations
                     .OfType<WaitAnnotation>()
                     .Select(wait => wait.Resource)
                     .OfType<IResourceWithConnectionString>()
                     .Distinct())
            e2eProjectBuilder.WithReference(builder.CreateResourceBuilder(connectionResource));
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
        var resourceBuilder = builder.CreateResourceBuilder((IResourceWithEndpoints)resource);
        if (!resource.Annotations.OfType<EndpointAnnotation>().Any(endpoint => endpoint.Name == "https"))
            resourceBuilder.WithHttpsEndpoint();

        // DCP ignores a proxied endpoint's declared public port whenever RandomizePorts is on, and the
        // Aspire testing builder always turns it on. An E2E host port is a contract the tests dial by
        // literal URL, so the endpoint has to be proxyless — otherwise DCP publishes the resource on
        // some other port and every call to the contract URL is refused.
        foreach (var endpoint in resource.Annotations
                     .OfType<EndpointAnnotation>()
                     .Where(endpoint => endpoint.Name == "https"))
        {
            endpoint.Port = port;
            endpoint.IsProxied = false;
        }
    }
}
