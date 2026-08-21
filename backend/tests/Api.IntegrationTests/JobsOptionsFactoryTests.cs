using DjVisualizer.Api.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace DjVisualizer.Api.IntegrationTests;

/// <summary>
/// Configuration on a deployed instance is entirely environment variables, typed by a human into
/// a hosting dashboard. Parsing must therefore be total: no input may throw, and no typo may be
/// read as the opposite of what was meant.
/// </summary>
public class JobsOptionsFactoryTests
{
    private static JobsOptions Create(Dictionary<string, string?> values, out IReadOnlyList<string> warnings) =>
        JobsOptionsFactory.Create(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            out warnings);

    [Fact]
    public void Uses_Documented_Defaults_When_Nothing_Is_Configured()
    {
        var options = Create([], out var warnings);

        options.Should().BeEquivalentTo(new JobsOptions());
        warnings.Should().BeEmpty();
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    [InlineData(" true ")]
    [InlineData("1")]
    [InlineData("yes")]
    [InlineData("on")]
    public void Accepts_The_Spellings_Of_True_People_Actually_Type(string raw)
    {
        var options = Create(new() { ["Jobs:SingleContainer"] = raw }, out var warnings);

        options.SingleContainer.Should().BeTrue();
        warnings.Should().BeEmpty();
    }

    [Theory]
    [InlineData("false")]
    [InlineData("0")]
    [InlineData("no")]
    [InlineData("off")]
    public void Accepts_The_Spellings_Of_False(string raw)
    {
        Create(new() { ["Jobs:SingleContainer"] = raw }, out _).SingleContainer.Should().BeFalse();
    }

    /// <summary>
    /// The consequence of getting this wrong is not cosmetic: read as true, a typo would start an
    /// API that also runs a render worker; read as false, it would start one that silently never
    /// renders anything. Falling back to the default and warning is the only safe answer.
    /// </summary>
    [Theory]
    [InlineData("truee")]
    [InlineData("ture")]
    [InlineData("enabled")]
    [InlineData("y")]
    [InlineData("-")]
    public void Falls_Back_And_Warns_On_An_Unrecognised_Flag_Rather_Than_Guessing(string raw)
    {
        var options = Create(new() { ["Jobs:SingleContainer"] = raw }, out var warnings);

        options.SingleContainer.Should().Be(new JobsOptions().SingleContainer);
        warnings.Should().ContainSingle().Which.Should().Contain("Jobs:SingleContainer").And.Contain(raw);
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("60MB")]
    [InlineData("1_000")]
    [InlineData("-1")]
    [InlineData("0")]
    [InlineData("1.5")]
    public void Falls_Back_And_Warns_On_A_Malformed_Or_Non_Positive_Size(string raw)
    {
        var options = Create(new() { ["Jobs:MaxAudioBytes"] = raw }, out var warnings);

        // Crucially not 0: UploadLimits rejects a non-positive limit, so reading a bad value as
        // zero would only move the crash a few lines later.
        options.MaxAudioBytes.Should().Be(new JobsOptions().MaxAudioBytes);
        warnings.Should().ContainSingle().Which.Should().Contain("Jobs:MaxAudioBytes");
    }

    [Fact]
    public void Parses_A_Complete_Deployment_Configuration()
    {
        var options = Create(new()
        {
            ["Jobs:RootPath"] = "/tmp/djvisualizer-jobs",
            ["Jobs:SingleContainer"] = "true",
            ["Jobs:MaxAudioBytes"] = "62914560",
            ["Jobs:MaxImageBytes"] = "10485760",
            ["Jobs:MaxDurationSeconds"] = "900",
            ["Jobs:MinFreeDiskBytes"] = "104857600",
        }, out var warnings);

        options.RootPath.Should().Be("/tmp/djvisualizer-jobs");
        options.SingleContainer.Should().BeTrue();
        options.MaxAudioBytes.Should().Be(62_914_560);
        options.MaxImageBytes.Should().Be(10_485_760);
        options.MaxDurationSeconds.Should().Be(900);
        options.MinFreeDiskBytes.Should().Be(104_857_600);
        warnings.Should().BeEmpty();
    }

    [Fact]
    public void Reports_Every_Malformed_Value_Not_Just_The_First()
    {
        Create(new()
        {
            ["Jobs:SingleContainer"] = "maybe",
            ["Jobs:MaxAudioBytes"] = "big",
            ["Jobs:MaxDurationSeconds"] = "ages",
        }, out var warnings);

        warnings.Should().HaveCount(3);
    }
}
