using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Infrastructure;
using PlaylistMiner.Infrastructure.Data;

if (args.Length < 1)
{
    Console.WriteLine("Usage: playlistminer <command> [options]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  import-takeout --path <folder>  Import Watch Later from Google Takeout");
    return 1;
}

var command = args[0];

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));

var connectionString = configuration.GetConnectionString("playlistminer")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__playlistminer")
    ?? throw new InvalidOperationException("Connection string 'playlistminer' not configured.");

services.AddDbContext<PlaylistMinerDbContext>(opts => opts.UseNpgsql(connectionString));
services.AddYouTubeIntegration(configuration);
services.AddApplicationServices();
services.AddHttpClient();

await using var provider = services.BuildServiceProvider();

switch (command)
{
    case "import-takeout":
        return await ImportTakeout(args, provider);
    default:
        Console.Error.WriteLine($"Unknown command: {command}");
        return 1;
}

static async Task<int> ImportTakeout(string[] args, ServiceProvider provider)
{
    var pathIndex = Array.IndexOf(args, "--path");
    if (pathIndex < 0 || pathIndex + 1 >= args.Length)
    {
        Console.Error.WriteLine("Usage: playlistminer import-takeout --path <folder>");
        return 1;
    }

    var folderPath = args[pathIndex + 1];
    if (!Directory.Exists(folderPath))
    {
        Console.Error.WriteLine($"Directory not found: {folderPath}");
        return 1;
    }

    Console.WriteLine($"Importing from: {folderPath}");

    var importService = provider.GetRequiredService<IImportService>();
    var result = await importService.ImportTakeoutFromPathAsync(folderPath);

    Console.WriteLine();
    Console.WriteLine($"Total videos found: {result.VideosFound}");
    Console.WriteLine($"Imported:           {result.VideosImported}");
    Console.WriteLine($"Skipped (existing): {result.Skipped}");
    Console.WriteLine($"Errors:             {result.Errors}");
    Console.WriteLine($"Batch ID:           {result.BatchId}");

    return result.Errors > 0 ? 1 : 0;
}
