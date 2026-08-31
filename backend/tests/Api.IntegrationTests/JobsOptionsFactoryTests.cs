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
            ["Jobs:MaxDurationSeconds"] = "600",
            ["Jobs:MinFreeDiskBytes"] = "104857600",
            ["Jobs:MaxEgressBytesPerWindow"] = "3221225472",
            ["Jobs:EgressWindowHours"] = "24",
        }, out var warnings);

        options.RootPath.Should().Be("/tmp/djvisualizer-jobs");
        options.SingleContainer.Should().BeTrue();
        options.MaxAudioBytes.Should().Be(62_914_560);
        options.MaxImageBytes.Should().Be(10_485_760);
        options.MaxDurationSeconds.Should().Be(600);
        options.MinFreeDiskBytes.Should().Be(104_857_600);
        options.MaxEgressBytesPerWindow.Should().Be(3_221_225_472);
        options.EgressWindowHours.Should().Be(24);
        warnings.Should().BeEmpty();
    }

    /// <summary>
    /// Every other size here rejects 0, because a zero upload limit is always a mistake. The
    /// egress budget is the exception: 0 is how a self-hosted instance says "do not cap this",
    /// and it is the default. Sharing the stricter parser would have turned that default into a
    /// warning and silently kept whatever fallback came with it.
    /// </summary>
    [Fact]
    public void Accepts_Zero_As_An_Unlimited_Egress_Budget()
    {
        var options = Create(new() { ["Jobs:MaxEgressBytesPerWindow"] = "0" }, out var warnings);

        options.MaxEgressBytesPerWindow.Should().Be(0);
        warnings.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("lots")]
    [InlineData("-1")]
    public void Falls_Back_And_Warns_On_A_Malformed_Egress_Budget(string raw)
    {
        var options = Create(new() { ["Jobs:MaxEgressBytesPerWindow"] = raw }, out var warnings);

        options.MaxEgressBytesPerWindow.Should().Be(new JobsOptions().MaxEgressBytesPerWindow);
        // An empty value is "unset", not "malformed" - it takes the default without complaint.
        warnings.Should().HaveCount(raw.Length == 0 ? 0 : 1);
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
