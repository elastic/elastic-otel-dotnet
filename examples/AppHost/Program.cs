// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

var builder = DistributedApplication.CreateBuilder(args);

builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://blah";

builder.AddProject<Projects.Example_AspNetCore_Mvc>("mvc");
builder.AddProject<Projects.Example_MinimalApi>("minimal-api");

builder.Build().Run();
