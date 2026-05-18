using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;

namespace PlaylistMiner.IntegrationTests.Api;

[Trait("Category", "Integration")]
public class ImportControllerTests
{
    [Fact]
    public async Task Test_UploadTakeout_Returns200_WithImportResult()
    {
        // Arrange
        var mockImport = new Mock<IImportService>();
        mockImport.Setup(s => s.ImportTakeoutAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportResult(2, 2, 0, 0, "batch123"));

        using var factory = new PlaylistMinerWebAppFactory(services =>
            services.AddSingleton(mockImport.Object));
        var client = factory.CreateClient();

        var csv = "Video ID,Playlist name,Created at\ndQw4w9WgXcQ,My Playlist,2024-01-01\n";
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(csv), "file", "takeout.csv");

        // Act
        var response = await client.PostAsync("/api/import/takeout", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ImportResult>();
        result.Should().NotBeNull();
        result!.VideosImported.Should().Be(2);
    }

    [Fact]
    public async Task Test_UploadTakeout_InvalidCsv_Returns400()
    {
        // Arrange
        var mockImport = new Mock<IImportService>();
        mockImport.Setup(s => s.ImportTakeoutAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportResult(0, 0, 0, 1, "batch456"));

        using var factory = new PlaylistMinerWebAppFactory(services =>
            services.AddSingleton(mockImport.Object));
        var client = factory.CreateClient();

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("bad content"), "file", "bad.csv");

        // Act
        var response = await client.PostAsync("/api/import/takeout", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Test_GetHistory_Returns200_WithBatches()
    {
        // Arrange
        var mockImport = new Mock<IImportService>();
        mockImport.Setup(s => s.GetHistoryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ImportBatch { Id = 1, Source = "Takeout", Filename = "batch1", TotalVideos = 5, ImportedCount = 5, FailedCount = 0, ImportedAt = DateTime.UtcNow }
            ]);

        using var factory = new PlaylistMinerWebAppFactory(services =>
            services.AddSingleton(mockImport.Object));
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/import/history");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var batches = await response.Content.ReadFromJsonAsync<List<ImportBatch>>();
        batches.Should().HaveCount(1);
    }
}
