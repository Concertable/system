using Concertable.AppHost;
using Concertable.Auth.Hosting;
using Concertable.B2B.Hosting;
using Concertable.Customer.Hosting;
using Concertable.Payment.Hosting;
using Concertable.Search.Hosting;

var builder = StrictDistributedApplication.CreateBuilder(args);
var manifest = CompatibilityManifest.Load(builder.AppHostDirectory);

var sql = builder.AddSqlServerContainer();
var b2bDb = sql.AddDatabase(B2BConstants.Database);
var authDb = sql.AddDatabase(AuthConstants.Database);
var customerDb = sql.AddDatabase(CustomerConstants.Database);
var searchDb = sql.AddDatabase(SearchConstants.Database);
var paymentDb = sql.AddDatabase(PaymentConstants.Database);
var (storage, blobs) = builder.AddAzureStorage();
var asb = builder.AddServiceBus();
asb.Topology().AddB2BTopology().AddCustomerTopology().AddSearchTopology().AddPaymentTopology().AddAuthTopology().RunAsEmulator();

// Every pinned image binds plaintext on its container port, but consumers resolve one another through
// GetEndpoint("https"), so the endpoint carrying that traffic must be named "https" while staying HTTP.
// Declaring it with WithHttpsEndpoint instead makes DCP fail to apply the container's configuration and
// the resource never starts, reporting no reason. AddPaymentWeb already does this inside its own
// package; these four still declare it here until their Hosting packages follow.
const int ContainerPort = 8080;

var (authImage, authDigest) = manifest["auth"];
var auth = builder.AddAuth(authImage, authDigest, authDb, asb)
                  .WithHttpEndpoint(targetPort: AuthConstants.ContainerPort, name: "https");
auth.WithSpaClients(SystemLocalSpaSurfaces.AuthClients);

var (paymentWebImage, paymentWebDigest) = manifest["payment-web"];
var paymentWeb = builder.AddPaymentWeb(paymentWebImage, paymentWebDigest, auth, paymentDb, asb);

var (b2bWebImage, b2bWebDigest) = manifest["b2b-web"];
var api = builder.AddB2BWeb(b2bWebImage, b2bWebDigest, b2bDb, auth, storage, blobs, asb, paymentWeb)
                 .WithHttpEndpoint(targetPort: ContainerPort, name: "https");
auth.WithEnvironment("Services__B2BApiUrl", api.GetEndpoint("https"));
auth.WithEnvironment("ServiceAuth__AuthClientId", "concertable-auth");

var (b2bWorkersImage, b2bWorkersDigest) = manifest["b2b-workers"];
var workers = builder.AddB2BWorkers(b2bWorkersImage, b2bWorkersDigest, b2bDb, paymentWeb, auth);

var (customerWebImage, customerWebDigest) = manifest["customer-web"];
var customerWeb = builder.AddCustomerWeb(customerWebImage, customerWebDigest, auth, customerDb, asb, paymentWeb)
                         .WithHttpEndpoint(targetPort: ContainerPort, name: "https");
auth.WithEnvironment("Services__CustomerApiUrl", customerWeb.GetEndpoint("https"));

// The payment image binds plaintext on the endpoint named "https", so every consumer of it must be
// told to accept that rather than negotiating TLS against a cleartext port.
if (builder.ExecutionContext.IsRunMode)
{
    api.WithEnvironment(PaymentConstants.AllowInsecureHttpClientEnvironmentVariable, bool.TrueString);
    workers.WithEnvironment(PaymentConstants.AllowInsecureHttpClientEnvironmentVariable, bool.TrueString);
    customerWeb.WithEnvironment(PaymentConstants.AllowInsecureHttpClientEnvironmentVariable, bool.TrueString);
}

var (searchWebImage, searchWebDigest) = manifest["search-web"];
builder.AddSearchWeb(searchWebImage, searchWebDigest, auth, searchDb)
       .WithHttpEndpoint(targetPort: ContainerPort, name: "https");

var (searchWorkersImage, searchWorkersDigest) = manifest["search-workers"];
builder.AddSearchWorkers(searchWorkersImage, searchWorkersDigest, searchDb, asb);

var (paymentWorkersImage, paymentWorkersDigest) = manifest["payment-workers"];
builder.AddPaymentWorkers(paymentWorkersImage, paymentWorkersDigest, paymentDb, asb);

builder.AddStripeCli(paymentWeb);
builder.Build().Run();
