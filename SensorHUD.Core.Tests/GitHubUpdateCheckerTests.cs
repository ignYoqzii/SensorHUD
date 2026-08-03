using System.Net;
using SensorHUD.Core.Updates;

namespace SensorHUD.Core.Tests;

public sealed class GitHubUpdateCheckerTests
{
    [Theory]
    [InlineData("v0.1.3", true)]
    [InlineData("0.1.2", false)]
    [InlineData("V0.1.2.0", false)]
    [InlineData("v0.1.1", false)]
    public async Task ComparesNormalizedReleaseVersion(
        string tag,
        bool expectedAvailable)
    {
        StubHttpMessageHandler handler = new(
            HttpStatusCode.OK,
            $$"""{"tag_name":"{{tag}}"}""");
        using HttpClient client = new(handler);
        GitHubUpdateChecker checker = new(client);

        UpdateCheckResult result = await checker.CheckAsync(
            new Version(0, 1, 2, 0));

        Assert.Equal(expectedAvailable, result.IsUpdateAvailable);
        Assert.Equal(
            "https://api.github.com/repos/ignYoqzii/SensorHUD/releases/latest",
            handler.RequestUri?.AbsoluteUri);
        Assert.Equal("SensorHUD/0.1.2", handler.UserAgent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("release")]
    [InlineData("v1")]
    [InlineData("v+1.2.3")]
    [InlineData("v1.2.3-beta")]
    [InlineData("v1.2.3.4.5")]
    public async Task RejectsInvalidReleaseTag(string tag)
    {
        using HttpClient client = new(new StubHttpMessageHandler(
            HttpStatusCode.OK,
            $$"""{"tag_name":"{{tag}}"}"""));
        GitHubUpdateChecker checker = new(client);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => checker.CheckAsync(new Version(1, 0, 0, 0)));
    }

    [Fact]
    public async Task PropagatesUnsuccessfulResponse()
    {
        using HttpClient client = new(new StubHttpMessageHandler(
            HttpStatusCode.Forbidden,
            "{}"));
        GitHubUpdateChecker checker = new(client);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => checker.CheckAsync(new Version(1, 0, 0, 0)));
    }

    [Fact]
    public void UsesStableLatestReleaseDownloadPage()
    {
        Assert.Equal(
            "https://github.com/ignYoqzii/SensorHUD/releases/latest",
            GitHubUpdateChecker.DownloadPageUri.AbsoluteUri);
    }

    private sealed class StubHttpMessageHandler(
        HttpStatusCode statusCode,
        string content) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string? UserAgent { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            UserAgent = request.Headers.UserAgent.ToString();
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content),
            });
        }
    }
}
