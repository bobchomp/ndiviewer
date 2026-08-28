using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NdiViewer.Update;

/// <summary>
/// Checks GitHub's "latest release" API for a newer NdiViewer-Setup-*.exe than the
/// one currently running. Every failure mode (offline, GitHub unreachable, malformed
/// response, no matching asset) is treated as "no update available" rather than an
/// error - this is a best-effort background check, not a critical operation.
/// </summary>
internal static class UpdateChecker
{
    private const string RepoOwner = "bobchomp";
    private const string RepoName = "ndiviewer";
    private const string ReleasesApiUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

    public static async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("NdiViewer-UpdateChecker");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            using HttpResponseMessage response = await http.GetAsync(ReleasesApiUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, cancellationToken: cancellationToken);
            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
            {
                return null;
            }

            string tag = release.TagName.TrimStart('v', 'V');
            if (!Version.TryParse(tag, out Version? latestVersion))
            {
                return null;
            }

            if (CompareVersions(latestVersion, GetCurrentVersion()) <= 0)
            {
                return null;
            }

            GitHubReleaseAsset? asset = release.Assets.FirstOrDefault(a =>
                a.Name.StartsWith("NdiViewer-Setup-", StringComparison.OrdinalIgnoreCase) &&
                a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

            if (asset is null || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
            {
                return null;
            }

            return new UpdateInfo(latestVersion, asset.BrowserDownloadUrl, asset.Name);
        }
        catch
        {
            // Offline, DNS failure, GitHub outage, rate limiting, malformed JSON, etc.
            // None of these should ever surface as an error to the user - they just
            // mean we couldn't confirm whether an update exists, so assume not.
            return null;
        }
    }

    private static Version GetCurrentVersion()
    {
        string? informationalVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion) && Version.TryParse(informationalVersion, out Version? parsed))
        {
            return parsed;
        }

        return Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
    }

    // System.Version.CompareTo treats an unset Revision (-1) as less than an explicit
    // 0, which would make an X.Y.Z tag compare as "older" than an X.Y.Z.0 assembly
    // version even though they're the same release - so only Major.Minor.Build matter.
    private static int CompareVersions(Version a, Version b)
    {
        int result = a.Major.CompareTo(b.Major);
        if (result != 0)
        {
            return result;
        }

        result = a.Minor.CompareTo(b.Minor);
        return result != 0 ? result : a.Build.CompareTo(b.Build);
    }
}
