using PlaylistMiner.Infrastructure;
using PlaylistMiner.Infrastructure.Data;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<PlaylistMinerDbContext>("playlistminer");

builder.Services.AddHostedService<PlaylistMiner.Worker.WorkerService>();
builder.Services.AddYouTubeIntegration(builder.Configuration);

var host = builder.Build();
host.Run();

