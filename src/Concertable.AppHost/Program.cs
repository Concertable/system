var builder = DistributedApplication.CreateBuilder(args);

var (sql, customerDb, searchDb) = builder.AddSqlServer();
var (storage, blobs) = builder.AddAzureStorage();
var asb = builder.AddServiceBus();

var auth = builder.AddAuth(sql);
var api = builder.AddApi(sql, auth, storage, blobs, asb);

builder.AddWorkers(sql);
builder.AddCustomerWeb(auth, customerDb, asb);
builder.AddSearchWeb(auth, searchDb);
builder.AddSearchWorkers(searchDb, asb);
builder.AddCustomerSpa(api, auth);
builder.AddVenueSpa(api, auth);
builder.AddArtistSpa(api, auth);
builder.AddBusinessSpa(api, auth);
builder.AddMobile(api, auth);
builder.AddStripeCli(api);

builder.Build().Run();
