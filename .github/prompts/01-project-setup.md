# Prompt 01: Project Scaffolding, .NET Aspire & Container Setup

## Context
We are building PlaylistMiner — a YouTube playlist organizer. This is the first step: creating the solution structure with .NET Aspire for orchestration and Podman for production containers. We follow Red-Green TDD with xUnit.

## Prompt 01a: Solution Structure

```
Create a .NET 10 solution called PlaylistMiner with .NET Aspire orchestration:

src/
  PlaylistMiner.AppHost/        - .NET Aspire AppHost (orchestrates all services for dev)
  PlaylistMiner.ServiceDefaults/ - Aspire ServiceDefaults (shared config: health checks, telemetry, resilience)
  PlaylistMiner.Api/            - ASP.NET Core Web API project (port 5000)
  PlaylistMiner.Worker/         - .NET Worker Service project with Quartz.NET
  PlaylistMiner.Core/           - Class library for domain models, interfaces, DTOs
  PlaylistMiner.Infrastructure/ - Class library for EF Core, YouTube client, repositories
  PlaylistMiner.CLI/            - Console app for CLI commands (import-takeout)

tests/
  PlaylistMiner.UnitTests/      - xUnit test project referencing Core and Infrastructure
  PlaylistMiner.IntegrationTests/ - xUnit test project with Testcontainers

Requirements:
- .NET 10, C# 13, nullable reference types enabled, implicit usings
- global.json pinning .NET SDK to 10.x

AppHost project:
- Reference: Aspire.Hosting.PostgreSQL, Aspire.Hosting.NodeJs
- Orchestrates: PostgreSQL (with pg_trgm), Api, Worker, Next.js frontend
- PostgreSQL provisioned via Aspire with volume mount for persistence
- Api and Worker receive PostgreSQL connection string via Aspire resource injection
- Next.js frontend added as npm app referencing Api for NEXT_PUBLIC_API_URL
- Ollama added as container resource with profile "ai" (optional)

ServiceDefaults project:
- Reference: Microsoft.Extensions.Http.Resilience, Microsoft.Extensions.ServiceDiscovery, OpenTelemetry exporters
- Configures: health checks, OpenTelemetry tracing/metrics, HTTP resilience defaults, service discovery
- Both Api and Worker reference ServiceDefaults

Api project:
- Reference: ServiceDefaults, Core, Infrastructure
- NuGet: Microsoft.AspNetCore.OpenApi, Microsoft.EntityFrameworkCore.Design
- Swagger/OpenAPI enabled in Development

Worker project:
- Reference: ServiceDefaults, Core, Infrastructure
- NuGet: Quartz.Extensions.Hosting

Infrastructure project:
- NuGet: Npgsql.EntityFrameworkCore.PostgreSQL, Microsoft.EntityFrameworkCore, Aspire.Npgsql.EntityFrameworkCore.PostgreSQL

Core project:
- ZERO NuGet dependencies (pure domain)

CLI project:
- Reference: Core, Infrastructure

UnitTests:
- NuGet: xunit, FluentAssertions, Moq, Microsoft.NET.Test.Sdk

IntegrationTests:
- NuGet: xunit, FluentAssertions, Testcontainers.PostgreSql, Microsoft.NET.Test.Sdk, Aspire.Hosting.Testing

Add:
- .editorconfig with standard C# conventions
- Directory.Build.props enabling nullable, implicit usings, TreatWarningsAsErrors

Do NOT add controllers, services, or models yet — just the empty structure.
```

## Prompt 01b: Aspire AppHost Configuration

```
In PlaylistMiner.AppHost/Program.cs, configure the Aspire orchestration:

var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL with pg_trgm extension and persistent volume
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("playlistminer-pgdata")
    .WithPgAdmin();

var db = postgres.AddDatabase("playlistminer");

// C# API
var api = builder.AddProject<Projects.PlaylistMiner_Api>("api")
    .WithReference(db)
    .WaitFor(db)
    .WithHttpEndpoint(port: 5000);

// C# Worker
builder.AddProject<Projects.PlaylistMiner_Worker>("worker")
    .WithReference(db)
    .WaitFor(db);

// Next.js frontend
builder.AddNpmApp("web", "../web", "dev")
    .WithReference(api)
    .WithHttpEndpoint(port: 3000)
    .WithEnvironment("NEXT_PUBLIC_API_URL", api.GetEndpoint("http"));

// Ollama (optional, behind feature flag)
if (builder.Configuration.GetValue<bool>("EnableOllama"))
{
    var ollama = builder.AddContainer("ollama", "ollama/ollama")
        .WithVolume("playlistminer-ollama", "/root/.ollama")
        .WithHttpEndpoint(port: 11434, targetPort: 11434);
}

builder.Build().Run();

In Api and Worker Program.cs, register Aspire service defaults:
- builder.AddServiceDefaults();
- builder.AddNpgsqlDbContext<PlaylistMinerDbContext>("playlistminer");
- app.MapDefaultEndpoints(); // health checks
```

## Prompt 01c: Podman Compose (Production)

```
Create podman-compose.yml for production deployment (Aspire handles dev):

Services: pm-db (postgres:16-alpine), pm-api (.NET 10), pm-worker (.NET 10), pm-web (Node 20), pm-ollama (optional, profile "ai").

Create multi-stage Dockerfiles for pm-api and pm-worker (sdk:10.0 build, aspnet:10.0 runtime).
Create Dockerfile for pm-web (node:20-alpine, next build, next start).
Create .env.example with all required variables.
Create .dockerignore.
```

## Verification
- `dotnet build` succeeds with zero warnings
- `dotnet run --project src/PlaylistMiner.AppHost` launches all services via Aspire dashboard
- Aspire dashboard shows: postgres, api, worker, web
- `dotnet test` runs (0 tests, no errors)
- `podman-compose up` starts production containers

## Next Step
Proceed to prompt 02 for database schema and migrations.
