using FluentAssertions;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Repositories;

namespace PlaylistMiner.IntegrationTests.Repositories;

[Trait("Category", "Integration")]
public class PlaylistRepositoryTests : RepositoryTestBase
{
    [Fact]
    public async Task Test_GetAll_IncludesVideoCounts()
    {
        // Arrange
        var repo = new PlaylistRepository(DbContext);
        var playlist = await repo.CreateAsync(MakePlaylist("PLcount1", "Count Playlist"));

        var v1 = MakeVideo("plcnt001", "PL Count Video 1");
        var v2 = MakeVideo("plcnt002", "PL Count Video 2");
        var v3 = MakeVideo("plcnt003", "PL Count Video 3");
        DbContext.Videos.AddRange(v1, v2, v3);
        await DbContext.SaveChangesAsync();

        DbContext.PlaylistVideos.AddRange([
            new PlaylistVideo { PlaylistId = playlist.Id, VideoId = v1.Id, Position = 0, AddedAt = DateTime.UtcNow },
            new PlaylistVideo { PlaylistId = playlist.Id, VideoId = v2.Id, Position = 1, AddedAt = DateTime.UtcNow },
            new PlaylistVideo { PlaylistId = playlist.Id, VideoId = v3.Id, Position = 2, AddedAt = DateTime.UtcNow }
        ]);
        await DbContext.SaveChangesAsync();

        // Act
        var playlists = await repo.GetAllAsync();

        // Assert
        var dto = playlists.First(p => p.Id == playlist.Id);
        dto.ItemCount.Should().Be(3);
    }

    [Fact]
    public async Task Test_SetInbox_ClearsPreviousInbox()
    {
        // Arrange
        var repo = new PlaylistRepository(DbContext);
        var playlistA = await repo.CreateAsync(MakePlaylist("PLBoxA01", "Inbox A"));
        var playlistB = await repo.CreateAsync(MakePlaylist("PLBoxB01", "Inbox B"));

        await repo.SetInboxAsync(playlistA.Id);

        // Act
        await repo.SetInboxAsync(playlistB.Id);

        // Assert
        DbContext.ChangeTracker.Clear();
        var a = await DbContext.Playlists.FindAsync(playlistA.Id);
        var b = await DbContext.Playlists.FindAsync(playlistB.Id);

        a!.IsInbox.Should().BeFalse();
        b!.IsInbox.Should().BeTrue();
    }

    [Fact]
    public async Task Test_GetInboxPlaylist_ReturnsDesignated()
    {
        // Arrange
        var repo = new PlaylistRepository(DbContext);
        var playlist = await repo.CreateAsync(MakePlaylist("PLInbox1", "My Inbox"));
        await repo.SetInboxAsync(playlist.Id);

        // Act
        var inbox = await repo.GetInboxAsync();

        // Assert
        inbox.Should().NotBeNull();
        inbox!.Id.Should().Be(playlist.Id);
        inbox.IsInbox.Should().BeTrue();
    }

    [Fact]
    public async Task Test_SetInbox_WhenPlaylistDoesNotExist_ThrowsKeyNotFoundException()
    {
        // Arrange
        var repo = new PlaylistRepository(DbContext);

        // Act
        var act = async () => await repo.SetInboxAsync(999);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*999*");
    }
}
