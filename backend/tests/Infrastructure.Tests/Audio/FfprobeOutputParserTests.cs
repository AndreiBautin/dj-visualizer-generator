using DjVisualizer.Application.Abstractions;
using DjVisualizer.Infrastructure.Audio;
using FluentAssertions;

namespace DjVisualizer.Infrastructure.Tests.Audio;

public class FfprobeOutputParserTests
{
    [Fact]
    public void ParseDuration_Reads_The_Duration_From_A_Well_Formed_Ffprobe_Response()
    {
        const string json = """{"format": {"duration": "125.484000"}}""";

        var duration = FfprobeOutputParser.ParseDuration(json);

        duration.Should().Be(TimeSpan.FromSeconds(125.484));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"format": {}}""")]
    [InlineData("""{"format": {"duration": "not-a-number"}}""")]
    public void ParseDuration_Throws_For_Unparseable_Output(string json)
    {
        var act = () => FfprobeOutputParser.ParseDuration(json);

        act.Should().Throw<AudioProbeException>();
    }
}
