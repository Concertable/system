using System.Threading.Tasks;
using Reqnroll;
using Xunit;

[CollectionDefinition("ReqnrollCollection", DisableParallelization = true)]
public sealed class ReqnrollCollection : ICollectionFixture<ReqnrollCollectionFixture>
{ }

public sealed class ReqnrollCollectionFixture : IAsyncLifetime
{
    public async Task InitializeAsync() =>
        await TestRunnerManager.OnTestRunStartAsync(GetType().Assembly);

    public async Task DisposeAsync() =>
        await TestRunnerManager.OnTestRunEndAsync(GetType().Assembly);
}
