var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer();

var api = builder.AddApi(sql);

builder.AddFrontend(api);

builder.Build().Run();
