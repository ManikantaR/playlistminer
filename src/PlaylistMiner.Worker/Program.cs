var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddHostedService<PlaylistMiner.Worker.WorkerService>();

var host = builder.Build();
host.Run();

