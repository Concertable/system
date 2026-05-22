var builder = DistributedApplication.CreateBuilder(args);

var (b2bDb, authDb, customerDb, searchDb, paymentDb) = builder.AddSqlServer();
var (storage, blobs) = builder.AddAzureStorage();
var asb = builder.AddServiceBus();

var auth = builder.AddAuth(authDb, b2bDb, asb);
var paymentWeb = builder.AddPaymentWeb(auth, paymentDb, asb);
var api = builder.AddApi(b2bDb, auth, storage, blobs, asb, paymentWeb);

auth.WithEnvironment("Services__B2BApiUrl", api.GetEndpoint("https"));
auth.WithEnvironment("ServiceAuth__AuthClientId", "concertable-auth");

builder.AddWorkers(b2bDb);
builder.AddCustomerWeb(auth, customerDb, asb, paymentWeb);
builder.AddSearchWeb(auth, searchDb);
builder.AddSearchWorkers(searchDb, asb);
builder.AddCustomerSpa(api, auth);
builder.AddVenueSpa(api, auth);
builder.AddArtistSpa(api, auth);
builder.AddBusinessSpa(api, auth);
builder.AddMobile(api, auth);
builder.AddStripeCli(api);

builder.Build().Run();
