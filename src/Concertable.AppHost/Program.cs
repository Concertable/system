var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer();
var (storage, blobs) = builder.AddAzureStorage();

var auth = builder.AddAuth(sql);

var api = builder.AddApi(sql, auth, storage, blobs);

builder.AddWorkers(sql);
builder.AddFrontend(api);
builder.AddStripeCli(api);

builder.Build().Run();
