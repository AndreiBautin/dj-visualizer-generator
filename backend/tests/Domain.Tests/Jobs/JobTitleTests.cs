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
    public void Create_Rejects_Empty_Or_Whitespace(string? value)
    {
        var act = () => JobTitle.Create(value!);

        act.Should().Throw<InvalidJobTitleException>();
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
}
