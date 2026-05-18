using PlaylistMiner.Infrastructure.Data;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<PlaylistMinerDbContext>("playlistminer");

builder.Services.AddHostedService<PlaylistMiner.Worker.WorkerService>();

var host = builder.Build();
host.Run();

