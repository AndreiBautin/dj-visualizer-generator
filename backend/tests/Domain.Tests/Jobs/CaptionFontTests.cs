using DjVisualizer.Domain.Exceptions;
using DjVisualizer.Domain.Jobs;
using FluentAssertions;

namespace DjVisualizer.Domain.Tests.Jobs;

public class CaptionFontTests
{
    [Theory]
    [InlineData("sans-bold")]
    [InlineData("serif-bold")]
    [InlineData("mono-bold")]
    public void FromName_Resolves_Known_Fonts(string name)
    {
        var act = () => CaptionFont.FromName(name);

        act.Should().NotThrow();
    }

    [Fact]
    public void FromName_Rejects_An_Unknown_Font()
    {
        var act = () => CaptionFont.FromName("comic-sans");

        act.Should().Throw<InvalidCaptionFontException>();
    }

    [Fact]
    public void Default_Is_SansBold()
    {
        CaptionFont.Default.Should().Be(CaptionFont.SansBold);
    }
}
