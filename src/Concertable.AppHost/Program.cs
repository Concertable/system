var builder = DistributedApplication.CreateBuilder(args);

var (sql, customerDb) = builder.AddSqlServer();
var (storage, blobs) = builder.AddAzureStorage();

var auth = builder.AddAuth(sql);
var api = builder.AddApi(sql, auth, storage, blobs);

builder.AddWorkers(sql);
builder.AddCustomerWeb(auth, customerDb);
builder.AddCustomerSpa(api, auth);
builder.AddVenueSpa(api, auth);
builder.AddArtistSpa(api, auth);
builder.AddBusinessSpa(api, auth);
builder.AddMobile(api, auth);
builder.AddStripeCli(api);

builder.Build().Run();
