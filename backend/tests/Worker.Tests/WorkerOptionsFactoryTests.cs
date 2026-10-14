using DjVisualizer.Worker.Hosting.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace DjVisualizer.Worker.Tests;

public class WorkerOptionsFactoryTests
{
    private static WorkerOptions Create(Dictionary<string, string?> values, out IReadOnlyList<string> warnings) =>
        WorkerOptionsFactory.Create(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), out warnings);

    [Fact]
    public void Unset_Values_Are_The_Documented_Defaults_Without_Warnings()
    {
        var options = Create([], out var warnings);

        options.Should().BeEquivalentTo(new WorkerOptions());
        warnings.Should().BeEmpty();
    }

    /// <summary>
    /// Each of these used to bind as given: a negative interval throws out of Task.Delay and kills
    /// the polling loop, a zero cleanup interval spins, a zero retention deletes videos before
    /// anyone can download them, and "abc" crashed the host at startup.
    /// </summary>
    [Theory]
    [InlineData("Worker:PollingIntervalSeconds", "-1")]
    [InlineData("Worker:PollingIntervalSeconds", "abc")]
    [InlineData("Worker:CleanupIntervalSeconds", "0")]
    [InlineData("Worker:RetentionMinutes", "0")]
    [InlineData("Worker:StaleProcessingMinutes", "-5")]
    [InlineData("Worker:EncoderThreads", "-2")]
    [InlineData("Worker:X264Preset", "turbo")]
    [InlineData("Worker:VideoCodec", "h265")]
    public void A_Bad_Value_Falls_Back_To_Its_Default_With_A_Warning_Naming_The_Key(string key, string value)
    {
        var options = Create(new() { [key] = value }, out var warnings);

        options.Should().BeEquivalentTo(new WorkerOptions());
        warnings.Should().ContainSingle().Which.Should().StartWith(key);
    }

    [Fact]
    public void Good_Values_Are_Used()
    {
        var options = Create(new()
        {
            ["Worker:PollingIntervalSeconds"] = "2",
            ["Worker:RetentionMinutes"] = "30",
            ["Worker:EncoderThreads"] = "1",
            ["Worker:X264Preset"] = "UltraFast",
            ["Worker:FontFilePathSansBold"] = "/fonts/a.ttf",
        }, out var warnings);

        warnings.Should().BeEmpty();
        options.PollingIntervalSeconds.Should().Be(2);
        options.RetentionMinutes.Should().Be(30);
        options.EncoderThreads.Should().Be(1);
        options.X264Preset.Should().Be("ultrafast");
        options.FontFilePathSansBold.Should().Be("/fonts/a.ttf");
    }
}
