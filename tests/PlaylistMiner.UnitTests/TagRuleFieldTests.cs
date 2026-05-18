using FluentAssertions;
using PlaylistMiner.Core.Models;

namespace PlaylistMiner.UnitTests;

public class TagRuleFieldTests
{
    [Fact]
    public void Test_TagRuleField_HasAllExpectedValues()
    {
        var values = Enum.GetValues<TagRuleField>();
        values.Should().Contain([
            TagRuleField.Title,
            TagRuleField.Description,
            TagRuleField.Both
        ]);
    }

    [Fact]
    public void Test_TagRule_DefaultField_IsBoth()
    {
        var rule = new TagRule
        {
            TagId = 1,
            Keyword = "react"
        };

        rule.Field.Should().Be(TagRuleField.Both);
    }

    [Fact]
    public void Test_TagRule_DefaultWeight_Is0Point5()
    {
        var rule = new TagRule
        {
            TagId = 1,
            Keyword = "react"
        };

        rule.Weight.Should().BeApproximately(0.5f, 0.001f);
    }
}
