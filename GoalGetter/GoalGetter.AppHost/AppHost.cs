var builder = DistributedApplication.CreateBuilder(args);

var cosmos = builder.AddAzureCosmosDB("cosmos-db").RunAsEmulator();

var apiService = builder.AddProject<Projects.GoalGetter_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

if (builder.Configuration["MatchPlayback:SpeedFactor"] is { } speedFactor)
{
    apiService.WithEnvironment("MatchPlayback__SpeedFactor", speedFactor);
}

builder.AddProject<Projects.GoalGetter_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
