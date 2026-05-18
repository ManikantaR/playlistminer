using System.Text;
using FluentAssertions;
using PlaylistMiner.Core.Import;

namespace PlaylistMiner.UnitTests.Import;

[Trait("Category", "Unit")]
public class TakeoutParserTests
{
    private static Stream ToStream(string content) =>
        new MemoryStream(Encoding.UTF8.GetBytes(content));

    [Fact]
    public void Test_Parse_ValidCsv_ReturnsVideoIds()
    {
        var csv = "Video Id,Time Added\ndQw4w9WgXcQ,2024-01-15\nabc123,2024-02-20";
        var entries = TakeoutParser.Parse(ToStream(csv));

        entries.Should().HaveCount(2);
        entries[0].VideoId.Should().Be("dQw4w9WgXcQ");
        entries[1].VideoId.Should().Be("abc123");
    }

    [Fact]
    public void Test_Parse_HandlesHeaderRow()
    {
        var csv = "Video Id,Time Added\nvid001,2024-01-01";
        var entries = TakeoutParser.Parse(ToStream(csv));

        entries.Should().HaveCount(1);
        entries[0].VideoId.Should().Be("vid001");
    }

    [Fact]
    public void Test_Parse_SkipsEmptyLines()
    {
        var csv = "Video Id,Time Added\nvid001,2024-01-01\n\n\nvid002,2024-01-02";
        var entries = TakeoutParser.Parse(ToStream(csv));

        entries.Should().HaveCount(2);
    }

    [Fact]
    public void Test_Parse_HandlesQuotedFields()
    {
        var csv = "Video Id,Time Added\n\"dQw4w9WgXcQ\",\"2024-01-15\"";
        var entries = TakeoutParser.Parse(ToStream(csv));

        entries.Should().HaveCount(1);
        entries[0].VideoId.Should().Be("dQw4w9WgXcQ");
    }

    [Fact]
    public void Test_Parse_InvalidFormat_ThrowsImportException()
    {
        var csv = "Name,Email\nJohn,john@example.com";
        var act = () => TakeoutParser.Parse(ToStream(csv));

        act.Should().Throw<ImportException>().WithMessage("*Video Id*");
    }

    [Fact]
    public void Test_Parse_EmptyFile_ThrowsImportException()
    {
        var csv = "";
        var act = () => TakeoutParser.Parse(ToStream(csv));

        act.Should().Throw<ImportException>().WithMessage("*empty*");
    }

    [Fact]
    public void Test_Parse_HeaderOnly_ThrowsImportException()
    {
        var csv = "Video Id,Time Added";
        var act = () => TakeoutParser.Parse(ToStream(csv));

        act.Should().Throw<ImportException>().WithMessage("*no video*");
    }

    [Fact]
    public void Test_Parse_ParsesTimeAdded()
    {
        var csv = "Video Id,Time Added\nvid001,2024-03-15";
        var entries = TakeoutParser.Parse(ToStream(csv));

        entries[0].AddedAt.Should().NotBeNull();
        entries[0].AddedAt!.Value.Year.Should().Be(2024);
        entries[0].AddedAt!.Value.Month.Should().Be(3);
    }

    [Fact]
    public void Test_Parse_VideoIdOnly_NoTimeColumn()
    {
        var csv = "Video Id\nvid001\nvid002";
        var entries = TakeoutParser.Parse(ToStream(csv));

        entries.Should().HaveCount(2);
        entries[0].AddedAt.Should().BeNull();
    }
}
