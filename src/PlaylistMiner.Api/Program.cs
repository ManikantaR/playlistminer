using PlaylistMiner.Infrastructure;
using PlaylistMiner.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<PlaylistMinerDbContext>("playlistminer");

builder.Services.AddOpenApi();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:3000").AllowAnyMethod().AllowAnyHeader()));
builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseCors();
app.MapDefaultEndpoints();
app.MapControllers();

app.Run();

public partial class Program { }
