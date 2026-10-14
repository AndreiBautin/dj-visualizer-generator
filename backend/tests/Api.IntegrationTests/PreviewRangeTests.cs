using DjVisualizer.Api.Downloads;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace DjVisualizer.Api.IntegrationTests;

public class PreviewRangeTests
{
    private static long Charge(string? range, long length = 1000, bool ifRange = false)
    {
        IHeaderDictionary headers = new HeaderDictionary();
        if (range is not null)
        {
            headers.Range = range;
        }

        if (ifRange)
        {
            headers.IfRange = "\"etag\"";
        }

        return PreviewRange.BytesToServe(headers, length);
    }

    [Theory]
    [InlineData("bytes=0-0", 1)]
    [InlineData("bytes=100-199", 100)]
    [InlineData("bytes=900-", 100)]
    [InlineData("bytes=-50", 50)]
    [InlineData("bytes=990-5000", 10)]
    [InlineData("bytes=-5000", 1000)]
    public void A_Single_Satisfiable_Range_Is_Charged_For_What_It_Sends(string range, long expected) =>
        Charge(range).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bytes=0-1,5-9")] // several ranges: ASP.NET answers with the whole file
    [InlineData("items=0-1")]
    [InlineData("nonsense")]
    public void Anything_Served_As_The_Whole_File_Is_Charged_In_Full(string? range) =>
        Charge(range).Should().Be(1000);

    [Fact]
    public void An_If_Range_Precondition_Is_Charged_In_Full_Because_A_Mismatch_Serves_Everything() =>
        Charge("bytes=0-0", ifRange: true).Should().Be(1000);

    [Fact]
    public void An_Unsatisfiable_Range_Sends_No_Body() =>
        Charge("bytes=5000-").Should().Be(0);
}
