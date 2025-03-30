var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.BookmarkFeeder_WebApi>("webapi");

builder.Build().Run();
