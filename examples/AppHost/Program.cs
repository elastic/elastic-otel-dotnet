var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Example_AspNetCore_Mvc>("mvc");
builder.AddProject<Projects.Example_MinimalApi>("minimal-api");

builder.Build().Run();
