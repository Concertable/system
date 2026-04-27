var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer();

var api = builder.AddApi(sql);

builder.AddWorkers(sql);
builder.AddFrontend(api);
builder.AddStripeCli(api);

builder.Build().Run();
