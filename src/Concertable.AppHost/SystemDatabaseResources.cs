using Aspire.Hosting.ApplicationModel;
using System.Security.Cryptography;
using System.Text;

namespace Concertable.AppHost;

internal static class SystemDatabaseResources
{
    extension(IDistributedApplicationBuilder builder)
    {
        public IResourceBuilder<SqlServerServerResource> AddSystemSqlServer()
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(AppContext.BaseDirectory));
            var checkoutSuffix = Convert.ToHexString(hash.AsSpan(0, 4)).ToLowerInvariant();
            return builder.AddSqlServer("sql")
                .WithDataVolume($"concertable-system-sql-data-{checkoutSuffix}");
        }
    }
}
