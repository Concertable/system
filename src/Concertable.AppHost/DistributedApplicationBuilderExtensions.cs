using Aspire.Hosting.Azure;

internal static class DistributedApplicationBuilderExtensions
{
    public static IResourceBuilder<SqlServerDatabaseResource> AddSqlServer(this IDistributedApplicationBuilder builder)
    {
        return builder.AddSqlServer("sql")
                      .WithDataVolume("concertable-sql-data")
                      .AddDatabase("DefaultConnection");
    }

    public static IResourceBuilder<ProjectResource> AddApi(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<SqlServerDatabaseResource> sql,
        IResourceBuilder<ProjectResource> auth)
    {
        return builder.AddProject<Projects.Concertable_Web>("api")
                      .WithReference(sql)
                      .WaitFor(sql)
                      .WithReference(auth)
                      .WaitFor(auth)
                      .WithEnvironment("Auth__Authority", auth.GetEndpoint("https"))
                      .AddSecrets(builder, "Stripe:WebhookSecret");
    }

    public static IResourceBuilder<ProjectResource> AddAuth(this IDistributedApplicationBuilder builder, IResourceBuilder<SqlServerDatabaseResource> sql)
    {
        return builder.AddProject<Projects.Concertable_Auth>("auth")
                      .WithReference(sql)
                      .WaitFor(sql);
    }

    public static IResourceBuilder<AzureFunctionsProjectResource> AddWorkers(this IDistributedApplicationBuilder builder, IResourceBuilder<SqlServerDatabaseResource> sql)
    {
        return builder.AddAzureFunctionsProject<Projects.Concertable_Workers>("workers")
                      .WithReference(sql)
                      .WaitFor(sql);
    }

    public static IResourceBuilder<NodeAppResource> AddFrontend(this IDistributedApplicationBuilder builder, IResourceBuilder<ProjectResource> api)
    {
        return builder.AddNpmApp("frontend", "../../app", "dev")
                      .WithHttpsEndpoint(port: 5173, isProxied: false)
                      .WithReference(api)
                      .WaitFor(api);
    }

    public static void AddStripeCli(this IDistributedApplicationBuilder builder, IResourceBuilder<ProjectResource> api)
    {
        var secretKey = builder.Configuration["Stripe:SecretKey"];
        if (string.IsNullOrEmpty(secretKey))
            return;

        builder.AddContainer("stripe-cli", "stripe/stripe-cli")
               .WithArgs("listen",
                   "--api-key", secretKey,
                   "--forward-to", "http://host.docker.internal:5161/api/webhook")
               .WithVolume("stripe-cli-config", "/root/.config/stripe")
               .WaitFor(api);
    }

    private static IResourceBuilder<ProjectResource> AddSecrets(this IResourceBuilder<ProjectResource> resource, IDistributedApplicationBuilder builder, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = builder.Configuration[key];
            if (!string.IsNullOrEmpty(value))
                resource = resource.WithEnvironment(key.Replace(":", "__"), value);
        }
        return resource;
    }
}
