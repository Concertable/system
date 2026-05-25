var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServerContainer();
var b2bDb = sql.AddDatabase("B2BDb");
var authDb = sql.AddDatabase("AuthDb");
var customerDb = sql.AddDatabase("CustomerDb");
var searchDb = sql.AddDatabase("SearchDb");
var paymentDb = sql.AddDatabase("PaymentDb");

var (storage, blobs) = builder.AddAzureStorage();
var asb = builder.AddServiceBus(b2b: true, customer: true, search: true, payment: true);

var auth = builder.AddAuth<Projects.Concertable_Auth>(authDb, b2bDb, asb);
var paymentWeb = builder.AddPaymentWeb<Projects.Concertable_Payment_Web>(auth, paymentDb, asb);
var api = builder.AddApi<Projects.Concertable_B2B_Web>(b2bDb, auth, storage, blobs, asb, paymentWeb);

auth.WithEnvironment("Services__B2BApiUrl", api.GetEndpoint("https"));
auth.WithEnvironment("ServiceAuth__AuthClientId", "concertable-auth");

builder.AddWorkers<Projects.Concertable_B2B_Workers>(b2bDb);
var customerWeb = builder.AddCustomerWeb<Projects.Concertable_Customer_Web>(auth, customerDb, asb, paymentWeb);
builder.AddSearchWeb<Projects.Concertable_Search_Web>(auth, searchDb);
builder.AddSearchWorkers<Projects.Concertable_Search_Workers>(searchDb, asb);
builder.AddPaymentWorkers<Projects.Concertable_Payment_Workers>(paymentDb, asb);
builder.AddCustomerSpa(api, customerWeb, auth);
builder.AddVenueSpa(api, auth);
builder.AddArtistSpa(api, auth);
builder.AddBusinessSpa(api, auth);
builder.AddMobile(api, auth);
builder.AddStripeCli(paymentWeb);

builder.Build().Run();
