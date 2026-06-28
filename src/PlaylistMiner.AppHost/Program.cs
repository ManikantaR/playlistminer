using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL with persistent volume and pgAdmin
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("playlistminer-pgdata")
    .WithPgAdmin();

var db = postgres.AddDatabase("playlistminer");

// C# API
var api = builder.AddProject<Projects.PlaylistMiner_Api>("api")
    .WithReference(db)
    .WaitFor(db)
    .WithHttpEndpoint(port: 5080);

// C# Worker
builder.AddProject<Projects.PlaylistMiner_Worker>("worker")
    .WithReference(db)
    .WaitFor(db);

// Next.js frontend
#pragma warning disable ASPIREJAVASCRIPT001
builder.AddNextJsApp("web", "../../web")
    .WithReference(api)
    .WithHttpEndpoint(port: 3000)
    .WithEnvironment("NEXT_PUBLIC_API_URL", api.GetEndpoint("http"));
#pragma warning restore ASPIREJAVASCRIPT001

// Ollama (optional, behind feature flag)
if (builder.Configuration.GetValue<bool>("EnableOllama"))
{
    builder.AddContainer("ollama", "ollama/ollama")
        .WithVolume("playlistminer-ollama", "/root/.ollama")
        .WithHttpEndpoint(port: 11434, targetPort: 11434);
}

builder.Build().Run();
