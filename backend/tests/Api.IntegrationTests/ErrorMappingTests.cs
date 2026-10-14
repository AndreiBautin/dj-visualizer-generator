using System.Reflection;
using DjVisualizer.Api.Controllers;
using DjVisualizer.Application.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace DjVisualizer.Api.IntegrationTests;

public class ErrorMappingTests
{
    public static TheoryData<string> AllErrorCodes()
    {
        var data = new TheoryData<string>();
        foreach (var field in typeof(ErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral))
        {
            data.Add((string)field.GetRawConstantValue()!);
        }

        return data;
    }

    /// <summary>The mapping used to end in a catch-all 502, so a new error code silently answered
    /// "bad gateway" whatever it meant. Each code now needs its own arm.</summary>
    [Theory]
    [MemberData(nameof(AllErrorCodes))]
    public void Every_Error_Code_Has_Its_Own_Http_Status(string code)
    {
        var result = JobsController.MapError(new Error(code, "message"));

        result.Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().BeInRange(400, 599);
    }

    [Fact]
    public void An_Unknown_Code_Is_A_Bug_Not_A_Guess()
    {
        var act = () => JobsController.MapError(new Error("made_up", "message"));

        act.Should().Throw<InvalidOperationException>();
    }
}
