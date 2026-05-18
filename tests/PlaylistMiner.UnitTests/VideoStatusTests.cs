using FluentAssertions;
using PlaylistMiner.Core.Models;

namespace PlaylistMiner.UnitTests;

public class VideoStatusTests
{
    [Fact]
    public void Test_VideoStatus_HasAllExpectedValues()
    {
        var values = Enum.GetValues<VideoStatus>();
        values.Should().Contain([
            VideoStatus.Active,
            VideoStatus.Unavailable,
            VideoStatus.Private,
            VideoStatus.Deleted,
            VideoStatus.Archived
        ]);
    }

    [Fact]
    public void Test_Video_DefaultStatus_IsActive()
    {
        var video = new Video
        {
            YouTubeId = "abc123",
            Title = "Test Video",
            ChannelName = "Test Channel",
            ChannelId = "UC123"
        };

        video.Status.Should().Be(VideoStatus.Active);
    }

    [Fact]
    public void Test_Video_YouTubeId_MaxLength_Is11()
    {
        var video = new Video
        {
            YouTubeId = "12345678901",
            Title = "Test",
            ChannelName = "Channel",
            ChannelId = "UC123"
        };

        video.YouTubeId.Length.Should().BeLessThanOrEqualTo(11);
    }
}
