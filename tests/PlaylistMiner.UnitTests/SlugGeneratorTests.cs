using FluentAssertions;
using PlaylistMiner.Core;

namespace PlaylistMiner.UnitTests;

public class SlugGeneratorTests
{
    [Theory]
    [InlineData("C#", "csharp")]
    [InlineData("ASP.NET Core", "aspnet-core")]
    [InlineData("React", "react")]
    [InlineData("Next.js", "nextjs")]
    [InlineData("GitHub Actions", "github-actions")]
    [InlineData("CI/CD", "ci-cd")]
    [InlineData("HTML/CSS", "html-css")]
    [InlineData("Machine Learning", "machine-learning")]
    [InlineData("LLMs", "llms")]
    [InlineData("Prompt Engineering", "prompt-engineering")]
    [InlineData("VS Code", "vs-code")]
    [InlineData("Tailwind CSS", "tailwind-css")]
    [InlineData("Node.js", "nodejs")]
    [InlineData("Vue", "vue")]
    [InlineData("TypeScript", "typescript")]
    public void Test_SlugGenerator_GeneratesExpectedSlug(string name, string expectedSlug)
    {
        var slug = SlugGenerator.Generate(name);
        slug.Should().Be(expectedSlug);
    }

    [Fact]
    public void Test_SlugGenerator_IsLowercase()
    {
        SlugGenerator.Generate("REACT").Should().Be("react");
    }

    [Fact]
    public void Test_SlugGenerator_TrimsWhitespace()
    {
        SlugGenerator.Generate("  React  ").Should().Be("react");
    }

    [Fact]
    public void Test_SlugGenerator_NormalizesMultipleSpaces()
    {
        SlugGenerator.Generate("Clean  Architecture").Should().Be("clean-architecture");
    }
}
