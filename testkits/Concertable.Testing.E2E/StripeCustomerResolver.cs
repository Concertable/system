using System.Net;
using Concertable.Seed.Identity;
using Stripe;

namespace Concertable.Testing.E2E;

public sealed class StripeCustomerResolver : IAsyncDisposable
{
    private const string CustomersKey = "E2EStripe:Customers";

    private static readonly Guid[] customerUserIds =
        SeedUsers.Managers.Select(manager => manager.Id)
            .Append(SeedCustomers.CustomerId(1))
            .ToArray();

    private readonly CustomerService customers;
    private readonly IReadOnlyDictionary<Guid, string> customerIds;
    private int disposed;

    public string RunId { get; }

    private StripeCustomerResolver(
        CustomerService customers,
        string runId,
        IReadOnlyDictionary<Guid, string> customerIds)
    {
        this.customers = customers;
        this.customerIds = customerIds;
        RunId = runId;
    }

    public static async Task<StripeCustomerResolver> CreateAsync(
        IStripeClient stripeClient,
        CancellationToken ct = default)
    {
        var customers = new CustomerService(stripeClient);
        var runId = Guid.NewGuid().ToString("N");
        var customerIds = new Dictionary<Guid, string>();

        try
        {
            foreach (var userId in customerUserIds)
            {
                var customer = await customers.CreateAsync(new CustomerCreateOptions
                {
                    Description = $"Concertable E2E {runId} / {userId:N}",
                    Metadata = new Dictionary<string, string>
                    {
                        ["concertableE2ERunId"] = runId,
                        ["concertableSeedUserId"] = userId.ToString("N"),
                    },
                }, cancellationToken: ct);
                customerIds.Add(userId, customer.Id);
            }

            return new StripeCustomerResolver(customers, runId, customerIds);
        }
        catch (Exception creationException)
        {
            var cleanupExceptions = await DeleteCustomersAsync(customers, customerIds.Values);
            if (cleanupExceptions.Count != 0)
                throw new AggregateException("Stripe E2E customer provisioning and cleanup both failed.",
                    [creationException, .. cleanupExceptions]);
            throw;
        }
    }

    public string Resolve(Guid userId) =>
        customerIds.TryGetValue(userId, out var customerId)
            ? customerId
            : throw new InvalidOperationException($"No Stripe customer was provisioned for seed user {userId} in E2E run {RunId}.");

    internal IReadOnlyDictionary<string, string> GetConfiguration()
    {
        var values = new Dictionary<string, string>();

        foreach (var (userId, customerId) in customerIds)
            values[$"{CustomersKey}:{userId:N}"] = customerId;

        return values;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;

        var cleanupExceptions = await DeleteCustomersAsync(customers, customerIds.Values);
        if (cleanupExceptions.Count != 0)
            throw new AggregateException("One or more Stripe E2E customers could not be deleted.", cleanupExceptions);
    }

    private static async Task<List<Exception>> DeleteCustomersAsync(
        CustomerService customers,
        IEnumerable<string> customerIds)
    {
        var exceptions = new List<Exception>();
        foreach (var customerId in customerIds)
        {
            try
            {
                await customers.DeleteAsync(customerId, cancellationToken: CancellationToken.None);
            }
            catch (StripeException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound) { }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        return exceptions;
    }
}
