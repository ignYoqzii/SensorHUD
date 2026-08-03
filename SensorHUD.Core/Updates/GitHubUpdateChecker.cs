using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SensorHUD.Core.Updates;

/// <summary>
/// Checks the latest stable GitHub release against an installed package
/// version. The release endpoint excludes drafts and prereleases.
/// </summary>
public sealed class GitHubUpdateChecker
{
    private static readonly Uri LatestReleaseApiUri = new(
        "https://api.github.com/repos/ignYoqzii/SensorHUD/releases/latest");

    public static Uri DownloadPageUri { get; } = new(
        "https://github.com/ignYoqzii/SensorHUD/releases/latest");

    private readonly HttpClient _httpClient;

    public GitHubUpdateChecker(HttpClient httpClient)
    {
        _httpClient = httpClient ??
            throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<UpdateCheckResult> CheckAsync(
        Version installedVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installedVersion);

        using HttpRequestMessage request = new(
            HttpMethod.Get,
            LatestReleaseApiUri);
        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/vnd.github+json"));
        request.Headers.UserAgent.Add(
            new ProductInfoHeaderValue(
                "SensorHUD",
                FormatVersion(installedVersion)));

        using HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using Stream content = await response.Content.ReadAsStreamAsync(
            cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(
            content,
            cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty(
                "tag_name",
                out JsonElement tagElement) ||
            tagElement.ValueKind != JsonValueKind.String ||
            !TryParseReleaseVersion(
                tagElement.GetString(),
                out Version? latestVersion))
        {
            throw new InvalidDataException(
                "The latest GitHub release has an invalid version tag.");
        }

        Version normalizedInstalled = NormalizeVersion(installedVersion);
        Version parsedLatestVersion = latestVersion!;
        return new UpdateCheckResult(
            parsedLatestVersion > normalizedInstalled,
            parsedLatestVersion);
    }

    private static bool TryParseReleaseVersion(
        string? tag,
        out Version? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        string value = tag.Trim();
        if (value.StartsWith('v') || value.StartsWith('V'))
        {
            value = value[1..];
        }

        string[] parts = value.Split('.');
        if (parts.Length is < 2 or > 4)
        {
            return false;
        }

        int[] numbers = new int[4];
        for (int index = 0; index < parts.Length; index++)
        {
            if (!int.TryParse(
                    parts[index],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out numbers[index]) ||
                numbers[index] < 0)
            {
                return false;
            }
        }

        version = new Version(
            numbers[0],
            numbers[1],
            numbers[2],
            numbers[3]);
        return true;
    }

    private static Version NormalizeVersion(Version version) => new(
        version.Major,
        Math.Max(0, version.Minor),
        Math.Max(0, version.Build),
        Math.Max(0, version.Revision));

    private static string FormatVersion(Version version) =>
        NormalizeVersion(version).ToString(3);
}

public sealed record UpdateCheckResult(
    bool IsUpdateAvailable,
    Version LatestVersion);
