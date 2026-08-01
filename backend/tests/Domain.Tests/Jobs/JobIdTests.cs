using DjVisualizer.Domain.Exceptions;
using DjVisualizer.Domain.Jobs;
using FluentAssertions;

namespace DjVisualizer.Domain.Tests.Jobs;

public class JobIdTests
{
    [Fact]
    public void New_Creates_Unique_Ids()
    {
        var first = JobId.New();
        var second = JobId.New();

        first.Should().NotBe(second);
    }

    [Fact]
    public void Parse_Roundtrips_A_Valid_Guid_String()
    {
        var original = JobId.New();

        var parsed = JobId.Parse(original.ToString());

        parsed.Should().Be(original);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("../../etc/passwd")]
    public void Parse_Rejects_Invalid_Input(string value)
    {
        var act = () => JobId.Parse(value);

        act.Should().Throw<InvalidJobIdException>();
    }
}
