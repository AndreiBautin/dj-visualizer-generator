using DjVisualizer.Api.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace DjVisualizer.Api.IntegrationTests;

/// <summary>
/// The SPA document's policy, exercised through the real middleware. The built SPA is not in
/// the test environment (wwwroot is a build output), so a stub endpoint stands in for
/// index.html - the policy is chosen by the response's content type, which is what matters.
/// </summary>
public class SecurityHeadersTests
{
    private static async Task<string> PolicyFor(string contentType)
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .Configure(app =>
                {
                    app.UseMiddleware<SecurityHeadersMiddleware>();
                    app.Run(context =>
                    {
                        context.Response.ContentType = contentType;
                        return context.Response.WriteAsync("x");
                    });
                }))
            .StartAsync();

        var response = await host.GetTestClient().GetAsync("/");
        return response.Headers.GetValues("Content-Security-Policy").Single();
    }

    [Fact]
    public async Task The_Spa_Document_May_Play_And_Show_Local_Blobs_But_Not_Run_Them()
    {
        var policy = await PolicyFor("text/html; charset=utf-8");

        // The audio preview and artwork preview read the visitor's own file via blob: URLs.
        policy.Should().Contain("media-src 'self' blob:");
        policy.Should().Contain("img-src 'self' data: blob:");
        // ...and that relaxation must never reach scripts.
        policy.Should().Contain("script-src 'self';");
    }

    [Fact]
    public async Task Api_Responses_Keep_The_Deny_Everything_Policy()
    {
        var policy = await PolicyFor("application/json");

        policy.Should().StartWith("default-src 'none'");
        policy.Should().NotContain("blob:");
    }

    /// <summary>Behind Render's edge the app itself only ever sees HTTP; the scheme the visitor used
    /// arrives in X-Forwarded-Proto, so HSTS depends on trusting that hop.</summary>
    [Fact]
    public async Task Hsts_Is_Sent_When_The_Trusted_Proxy_Says_The_Visitor_Used_Https()
    {
        using var factory = new Support.JobsApiFactory { ExtraConfiguration = { ["Hosting:TrustForwardedFor"] = "true" } };
        using var client = factory.CreateClient();

        using var overHttps = new HttpRequestMessage(HttpMethod.Get, "/health");
        overHttps.Headers.Add("X-Forwarded-Proto", "https");
        (await client.SendAsync(overHttps)).Headers.GetValues("Strict-Transport-Security").Single().Should().Be("max-age=31536000");

        (await client.GetAsync("/health")).Headers.Contains("Strict-Transport-Security").Should().BeFalse("plain HTTP must not claim HTTPS");
    }

    [Fact]
    public async Task Hsts_Ignores_A_Forwarded_Scheme_Nobody_Is_Trusted_To_Set()
    {
        using var factory = new Support.JobsApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Forwarded-Proto", "https");

        (await client.SendAsync(request)).Headers.Contains("Strict-Transport-Security").Should().BeFalse();
    }
}
