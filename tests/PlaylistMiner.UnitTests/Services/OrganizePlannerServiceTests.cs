using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PlaylistMiner.Core.Categorization;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;
using PlaylistMiner.Infrastructure.Services;

namespace PlaylistMiner.UnitTests.Services;

[Trait("Category", "Unit")]
public class OrganizePlannerServiceTests
{
    private static PlaylistMinerDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<PlaylistMinerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlaylistMinerDbContext(opts);
    }

    private static OrganizePlannerService CreateService(
        PlaylistMinerDbContext db,
        CategorizationOptions? options = null)
    {
        var effectiveOptions = options ?? new CategorizationOptions();
        return new(
            db,
            Options.Create(effectiveOptions),
            CreatePolicyService(effectiveOptions.AutoFileConfidence),
            NullLogger<OrganizePlannerService>.Instance);
    }

    private static IAutomationPolicyService CreatePolicyService(float highConfidenceThreshold)
    {
        var policy = new AutomationPolicyDto(
            "aggressive_with_undo",
            highConfidenceThreshold,
            0.65f,
            80,
            150,
            5,
            "23:00",
            "05:00",
            false,
            null,
            null,
            "never",
            false);
        var policyService = new Mock<IAutomationPolicyService>();
        policyService.Setup(x => x.GetPolicyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);
        return policyService.Object;
    }

    [Fact]
    public async Task Test_BuildPlan_WhenInboxVideoMatchesExistingManagedPlaylist_PlansMove()
    {
        using var db = CreateDb();
        var now = DateTime.UtcNow;

        var inbox = new Playlist
        {
            Id = 1,
            YouTubeId = "PLinbox",
            Name = "Incoming",
            IsInbox = true,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };
        var managed = new Playlist
        {
            Id = 2,
            YouTubeId = "PLmanaged",
            Name = "AI Agents",
            IsManaged = true,
            Topic = "AI Agents",
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };
        var tag = new Tag
        {
            Id = 10,
            Name = "AI Agents",
            Slug = "ai-agents",
            CreatedAt = now
        };
        var video = new Video
        {
            Id = 100,
            YouTubeId = "vid001",
            Title = "Agentic Systems",
            Description = "desc",
            ChannelName = "Channel",
            ChannelId = "UC1",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            Duration = TimeSpan.FromMinutes(10),
            PublishedAt = now,
            Status = VideoStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };

        db.Playlists.AddRange(inbox, managed);
        db.Tags.Add(tag);
        db.Videos.Add(video);
        db.PlaylistVideos.Add(new PlaylistVideo
        {
            PlaylistId = inbox.Id,
            VideoId = video.Id,
            Position = 0,
            AddedAt = now,
            PlaylistItemId = "pli-inbox"
        });
        db.VideoTags.Add(new VideoTag
        {
            VideoId = video.Id,
            TagId = tag.Id,
            Source = TagSource.Ollama,
            Confidence = 0.91f,
            CreatedAt = now
        });
        await db.SaveChangesAsync();

        IOrganizePlannerService service = CreateService(db);

        var plan = await service.BuildPlanAsync();

        plan.VideosExamined.Should().Be(1);
        plan.Items.Should().ContainSingle();
        plan.Items[0].Action.Should().Be("move");
        plan.Items[0].TargetPlaylistId.Should().Be(managed.Id);
        plan.Items[0].TargetPlaylistName.Should().Be("AI Agents");
        plan.Items[0].Topic.Should().Be("AI Agents");
        plan.Items[0].EstimatedQuotaCost.Should().Be(100);
    }

    [Fact]
    public async Task Test_BuildPlan_WhenManagedPlaylistMissing_PlansPlaylistCreationThenMove()
    {
        using var db = CreateDb();
        var now = DateTime.UtcNow;

        var inbox = new Playlist
        {
            Id = 1,
            YouTubeId = "PLinbox",
            Name = "Incoming",
            IsInbox = true,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };
        var tag = new Tag
        {
            Id = 10,
            Name = "Distributed Systems",
            Slug = "distributed-systems",
            CreatedAt = now
        };
        var video = new Video
        {
            Id = 100,
            YouTubeId = "vid001",
            Title = "Queues and Streams",
            Description = "desc",
            ChannelName = "Channel",
            ChannelId = "UC1",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            Duration = TimeSpan.FromMinutes(10),
            PublishedAt = now,
            Status = VideoStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };

        db.Playlists.Add(inbox);
        db.Tags.Add(tag);
        db.Videos.Add(video);
        db.PlaylistVideos.Add(new PlaylistVideo
        {
            PlaylistId = inbox.Id,
            VideoId = video.Id,
            Position = 0,
            AddedAt = now,
            PlaylistItemId = "pli-inbox"
        });
        db.VideoTags.Add(new VideoTag
        {
            VideoId = video.Id,
            TagId = tag.Id,
            Source = TagSource.RuleBased,
            Confidence = 0.87f,
            CreatedAt = now
        });
        await db.SaveChangesAsync();

        IOrganizePlannerService service = CreateService(db);

        var plan = await service.BuildPlanAsync();

        plan.Items.Should().HaveCount(2);
        plan.Items[0].Action.Should().Be("create_playlist");
        plan.Items[0].TargetPlaylistName.Should().Be("Distributed Systems");
        plan.Items[0].EstimatedQuotaCost.Should().Be(50);
        plan.Items[1].Action.Should().Be("move");
        plan.Items[1].TargetPlaylistName.Should().Be("Distributed Systems");
        plan.Items[1].EstimatedQuotaCost.Should().Be(100);
    }

    [Fact]
    public async Task Test_BuildPlan_WhenConfidenceBelowThreshold_PlansReview()
    {
        using var db = CreateDb();
        var now = DateTime.UtcNow;

        var inbox = new Playlist
        {
            Id = 1,
            YouTubeId = "PLinbox",
            Name = "Incoming",
            IsInbox = true,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };
        var tag = new Tag
        {
            Id = 10,
            Name = "TypeScript",
            Slug = "typescript",
            CreatedAt = now
        };
        var video = new Video
        {
            Id = 100,
            YouTubeId = "vid001",
            Title = "Tentative TypeScript Intro",
            Description = "desc",
            ChannelName = "Channel",
            ChannelId = "UC1",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            Duration = TimeSpan.FromMinutes(10),
            PublishedAt = now,
            Status = VideoStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };

        db.Playlists.Add(inbox);
        db.Tags.Add(tag);
        db.Videos.Add(video);
        db.PlaylistVideos.Add(new PlaylistVideo
        {
            PlaylistId = inbox.Id,
            VideoId = video.Id,
            Position = 0,
            AddedAt = now,
            PlaylistItemId = "pli-inbox"
        });
        db.VideoTags.Add(new VideoTag
        {
            VideoId = video.Id,
            TagId = tag.Id,
            Source = TagSource.TfIdf,
            Confidence = 0.42f,
            CreatedAt = now
        });
        await db.SaveChangesAsync();

        IOrganizePlannerService service = CreateService(db);

        var plan = await service.BuildPlanAsync();

        plan.Items.Should().ContainSingle();
        plan.Items[0].Action.Should().Be("review");
        plan.Items[0].EstimatedQuotaCost.Should().Be(0);
        plan.Items[0].TargetPlaylistName.Should().BeNull();
        plan.Items[0].Reason.Should().Contain("confidence");
    }

    [Fact]
    public async Task Test_BuildPlan_UsesSharedAutoFileConfidenceThresholdBoundary()
    {
        using var db = CreateDb();
        var now = DateTime.UtcNow;

        var inbox = new Playlist
        {
            Id = 1,
            YouTubeId = "PLinbox",
            Name = "Incoming",
            IsInbox = true,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };
        var managed = new Playlist
        {
            Id = 2,
            YouTubeId = "PLmanaged",
            Name = "AI Agents",
            IsManaged = true,
            Topic = "AI Agents",
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };
        var tag = new Tag
        {
            Id = 10,
            Name = "AI Agents",
            Slug = "ai-agents",
            CreatedAt = now
        };
        var video = new Video
        {
            Id = 100,
            YouTubeId = "vid001",
            Title = "Agentic Systems",
            Description = "desc",
            ChannelName = "Channel",
            ChannelId = "UC1",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            Duration = TimeSpan.FromMinutes(10),
            PublishedAt = now,
            Status = VideoStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };

        db.Playlists.AddRange(inbox, managed);
        db.Tags.Add(tag);
        db.Videos.Add(video);
        db.PlaylistVideos.Add(new PlaylistVideo
        {
            PlaylistId = inbox.Id,
            VideoId = video.Id,
            Position = 0,
            AddedAt = now,
            PlaylistItemId = "pli-inbox"
        });
        db.VideoTags.Add(new VideoTag
        {
            VideoId = video.Id,
            TagId = tag.Id,
            Source = TagSource.Ollama,
            Confidence = 0.70f,
            CreatedAt = now
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, new CategorizationOptions { AutoFileConfidence = 0.70f });

        var plan = await service.BuildPlanAsync();

        plan.Items.Should().ContainSingle();
        plan.Items[0].Action.Should().Be("move");
        plan.Items[0].Confidence.Should().BeApproximately(0.70f, 0.001f);
    }

    [Fact]
    public async Task Test_BuildPlan_UsesPersistedAutomationHighConfidenceThreshold()
    {
        using var db = CreateDb();
        var now = DateTime.UtcNow;

        var inbox = new Playlist
        {
            Id = 1,
            YouTubeId = "PLinbox",
            Name = "Incoming",
            IsInbox = true,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };
        var managed = new Playlist
        {
            Id = 2,
            YouTubeId = "PLmanaged",
            Name = "AI Agents",
            IsManaged = true,
            Topic = "AI Agents",
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };
        var tag = new Tag
        {
            Id = 10,
            Name = "AI Agents",
            Slug = "ai-agents",
            CreatedAt = now
        };
        var video = new Video
        {
            Id = 100,
            YouTubeId = "vid001",
            Title = "Agentic Systems",
            Description = "desc",
            ChannelName = "Channel",
            ChannelId = "UC1",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            Duration = TimeSpan.FromMinutes(10),
            PublishedAt = now,
            Status = VideoStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };

        db.Playlists.AddRange(inbox, managed);
        db.Tags.Add(tag);
        db.Videos.Add(video);
        db.PlaylistVideos.Add(new PlaylistVideo
        {
            PlaylistId = inbox.Id,
            VideoId = video.Id,
            Position = 0,
            AddedAt = now,
            PlaylistItemId = "pli-inbox"
        });
        db.VideoTags.Add(new VideoTag
        {
            VideoId = video.Id,
            TagId = tag.Id,
            Source = TagSource.Ollama,
            Confidence = 0.70f,
            CreatedAt = now
        });
        db.Settings.Add(new Setting
        {
            Key = "automation.high_confidence_threshold",
            Value = "0.8",
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        var service = new OrganizePlannerService(
            db,
            Options.Create(new CategorizationOptions { AutoFileConfidence = 0.70f }),
            new AutomationPolicyService(db),
            NullLogger<OrganizePlannerService>.Instance);

        var plan = await service.BuildPlanAsync();

        plan.Items.Should().ContainSingle();
        plan.Items[0].Action.Should().Be("review");
        plan.Items[0].Reason.Should().Contain("threshold");
    }

    [Fact]
    public async Task Test_BuildPlan_WhenMultipleTopicsQualify_ChoosesSingleWinningTopic()
    {
        using var db = CreateDb();
        var now = DateTime.UtcNow;

        var inbox = new Playlist
        {
            Id = 1,
            YouTubeId = "PLinbox",
            Name = "Incoming",
            IsInbox = true,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };
        var aiTag = new Tag
        {
            Id = 10,
            Name = "AI Agents",
            Slug = "ai-agents",
            CreatedAt = now
        };
        var mlTag = new Tag
        {
            Id = 11,
            Name = "Machine Learning",
            Slug = "machine-learning",
            CreatedAt = now
        };
        var video = new Video
        {
            Id = 100,
            YouTubeId = "vid001",
            Title = "Agentic ML Systems",
            Description = "desc",
            ChannelName = "Channel",
            ChannelId = "UC1",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            Duration = TimeSpan.FromMinutes(10),
            PublishedAt = now,
            Status = VideoStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };

        db.Playlists.Add(inbox);
        db.Tags.AddRange(aiTag, mlTag);
        db.Videos.Add(video);
        db.PlaylistVideos.Add(new PlaylistVideo
        {
            PlaylistId = inbox.Id,
            VideoId = video.Id,
            Position = 0,
            AddedAt = now,
            PlaylistItemId = "pli-inbox"
        });
        db.VideoTags.AddRange(
            new VideoTag
            {
                VideoId = video.Id,
                TagId = aiTag.Id,
                Source = TagSource.Ollama,
                Confidence = 0.93f,
                CreatedAt = now
            },
            new VideoTag
            {
                VideoId = video.Id,
                TagId = mlTag.Id,
                Source = TagSource.Ollama,
                Confidence = 0.81f,
                CreatedAt = now
            });
        await db.SaveChangesAsync();

        var service = CreateService(db, new CategorizationOptions { AutoFileConfidence = 0.70f });

        var plan = await service.BuildPlanAsync();

        plan.Items.Should().HaveCount(2);
        plan.Items[0].Action.Should().Be("create_playlist");
        plan.Items[0].TargetPlaylistName.Should().Be("AI Agents");
        plan.Items[1].Action.Should().Be("move");
        plan.Items[1].Topic.Should().Be("AI Agents");
        plan.Items[1].TargetPlaylistName.Should().Be("AI Agents");
        plan.Items[1].Reason.Should().Contain("single topic");
        plan.Items.Should().NotContain(item => item.TargetPlaylistName == "Machine Learning");
    }
}
