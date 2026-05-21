var builder = DistributedApplication.CreateBuilder(args);

var (sql, customerDb, searchDb, paymentDb) = builder.AddSqlServer();
var (storage, blobs) = builder.AddAzureStorage();
var asb = builder.AddServiceBus();

var auth = builder.AddAuth(sql);
var paymentWeb = builder.AddPaymentWeb(auth, paymentDb, asb);
builder.AddPaymentWorkers(paymentDb, asb);
var api = builder.AddApi(sql, auth, storage, blobs, asb, paymentWeb);

builder.AddWorkers(sql);
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
