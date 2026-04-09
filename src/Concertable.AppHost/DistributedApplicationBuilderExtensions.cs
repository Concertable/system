using Aspire.Hosting.Azure;

internal static class DistributedApplicationBuilderExtensions
{
    public static IResourceBuilder<SqlServerDatabaseResource> AddSqlServer(this IDistributedApplicationBuilder builder)
    {
        return builder.AddSqlServer("sql")
                      .WithDataVolume("concertable-sql-data")
                      .AddDatabase("DefaultConnection");
    }

    public static IResourceBuilder<ProjectResource> AddApi(this IDistributedApplicationBuilder builder, IResourceBuilder<SqlServerDatabaseResource> sql)
    {
        return builder.AddProject<Projects.Concertable_Web>("api")
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
}
