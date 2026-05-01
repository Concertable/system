var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer();

var auth = builder.AddAuth(sql);

var api = builder.AddApi(sql, auth);

builder.AddWorkers(sql);
builder.AddFrontend(api);
builder.AddStripeCli(api);

builder.Build().Run();
