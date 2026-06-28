using Microsoft.EntityFrameworkCore;
using PlaylistMiner.Infrastructure;
using PlaylistMiner.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<PlaylistMinerDbContext>("playlistminer");

builder.Services.AddOpenApi();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:3000", "http://localhost:3001").AllowAnyMethod().AllowAnyHeader()));
builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PlaylistMinerDbContext>();
    if (db.Database.IsRelational())
        await db.Database.MigrateAsync();
    else
        await db.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseCors();
app.MapDefaultEndpoints();

// Explicit health endpoint (works in Production, unlike Aspire's dev-only /health).
// Verifies DB connectivity so Uptime Kuma + deploy script report real health.
app.MapGet("/api/health", async (PlaylistMinerDbContext db, CancellationToken ct) =>
{
    var dbOk = await db.Database.CanConnectAsync(ct);
    return dbOk
        ? Results.Ok(new { status = "healthy", db = "up" })
        : Results.Json(new { status = "degraded", db = "down" }, statusCode: 503);
});

app.MapControllers();

app.Run();

public partial class Program { }
