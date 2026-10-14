using DjVisualizer.Domain.Exceptions;
using DjVisualizer.Domain.Jobs;
using FluentAssertions;

namespace DjVisualizer.Domain.Tests.Jobs;

public class JobTitleTests
{
    [Fact]
    public void Create_Trims_Surrounding_Whitespace()
    {
        var title = JobTitle.Create("  Friday Night Set  ");

        title.Value.Should().Be("Friday Night Set");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_Reads_Empty_Or_Whitespace_As_No_Title(string? value)
    {
        var title = JobTitle.Create(value);

        title.Should().BeSameAs(JobTitle.None);
        title.HasText.Should().BeFalse();
    }

    [Fact]
    public void Create_Rejects_Titles_Longer_Than_200_Characters()
    {
        var tooLong = new string('a', 201);

        var act = () => JobTitle.Create(tooLong);

        act.Should().Throw<InvalidJobTitleException>();
    }

    [Fact]
    public void Create_Accepts_Titles_Up_To_200_Characters()
    {
        var maxLength = new string('a', 200);

        var title = JobTitle.Create(maxLength);

        title.Value.Should().HaveLength(200);
    }

    [Theory]
    [InlineData("mix\u202Ekcod.mp4")] // right-to-left override: spoofs the displayed extension
    [InlineData("a\u2066b")]
    [InlineData("two\nlines")]
    [InlineData("tab\there")]
    [InlineData("nul\0byte")]
    [InlineData("line\u2028sep")]
    public void Create_Rejects_Control_And_Direction_Override_Characters(string value)
    {
        var act = () => JobTitle.Create(value);

        act.Should().Throw<InvalidJobTitleException>();
    }

    [Theory]
    [InlineData("Caf\u00E9 Del Mar")]
    [InlineData("\u0645\u0632\u064A\u062C")] // right-to-left script itself is fine
    [InlineData("\U0001F468\u200D\U0001F3A4 set")] // zero-width joiner inside an emoji sequence
    public void Create_Keeps_Ordinary_International_Text(string value) =>
        JobTitle.Create(value).Value.Should().Be(value);
}
