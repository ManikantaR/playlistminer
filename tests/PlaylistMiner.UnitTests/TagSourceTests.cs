using FluentAssertions;
using PlaylistMiner.Core.Models;

namespace PlaylistMiner.UnitTests;

public class TagSourceTests
{
    [Fact]
    public void Test_TagSource_HasAllExpectedValues()
    {
        var values = Enum.GetValues<TagSource>();
        values.Should().Contain([
            TagSource.Manual,
            TagSource.RuleBased,
            TagSource.TfIdf,
            TagSource.Ollama,
            TagSource.Suggested
        ]);
    }
}
