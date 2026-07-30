// MBBSLauncher - Update Checker
// Created by Mark Laudenbach with Love in Iowa
// https://github.com/SysopNetwork/MBBSLauncher
//
// File: Core/UpdateChecker.cs
// Version: v2.0-beta12
//
// Change History:
// 26.07.03.1 - v2.0-beta12 - Initial creation. MANUAL-ONLY update check. Queries the GitHub
//                     Releases API for the newest published release, compares its tag against the
//                     running version, and returns the result for the sysop to review. This class
//                     performs NO work unless CheckForUpdatesAsync() is called, and the only caller
//                     is the "Check for Updates" button on the Configuration editor's Advanced tab.
//                     Nothing here runs at startup or on any timer — the launcher never "calls home"
//                     on its own. The check only reports; it never downloads or installs anything.

using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MBBSLauncher.Core
{
    /// <summary>
    /// Result of a manual update check. Success indicates the check completed (network + parse);
    /// it does not imply an update exists — inspect UpdateAvailable for that.
    /// </summary>
    public class UpdateCheckResult
    {
        /// <summary>True if the check reached GitHub and parsed a release successfully.</summary>
        public bool Success { get; set; }

        /// <summary>Human-readable reason the check failed (null when Success is true).</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>The version this launcher is running (as passed in, e.g. "v2.0-beta12").</summary>
        public string CurrentVersion { get; set; } = "";

        /// <summary>The tag of the newest published release (e.g. "v1.85"), empty on failure.</summary>
        public string LatestVersion { get; set; } = "";

        /// <summary>The release title from GitHub (e.g. "MBBSLauncher v1.85").</summary>
        public string ReleaseTitle { get; set; } = "";

        /// <summary>The GitHub release page URL the sysop can open to download the update.</summary>
        public string DownloadUrl { get; set; } = "";

        /// <summary>True if the newest published release is marked as a pre-release on GitHub.</summary>
        public bool IsPrerelease { get; set; }

        /// <summary>True when the newest published release is newer than the running version.</summary>
        public bool UpdateAvailable { get; set; }

        /// <summary>
        /// True when the running version is NEWER than the newest published release (e.g. running an
        /// unpublished beta build). UpdateAvailable is false in this case.
        /// </summary>
        public bool RunningNewerThanPublished { get; set; }
    }

    /// <summary>
    /// Manual, on-demand check for a newer MBBSLauncher release on GitHub.
    /// IMPORTANT: This is a sysop-initiated check only. It is never wired to startup, a timer, or any
    /// background task — the launcher does not contact GitHub unless the sysop clicks the button.
    /// </summary>
    public static class UpdateChecker
    {
        // GitHub Releases API for the MBBSLauncher repo. The list endpoint returns every release
        // (newest first, including pre-releases) so the beta channel is covered — unlike
        // /releases/latest, which silently skips pre-releases.
        private const string ReleasesApiUrl =
            "https://api.github.com/repos/SysopNetwork/MBBSLauncher/releases";

        // GitHub requires a User-Agent on every API request; it rejects requests without one.
        private const string UserAgent = "MBBSLauncher-UpdateCheck";

        // Give up rather than hang the UI if GitHub is slow or unreachable.
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Contacts GitHub, finds the newest published release, and compares it to <paramref name="currentVersion"/>.
        /// Runs entirely on a background thread; safe to await from a UI event handler.
        /// Never throws — all failures are reported through the returned result's ErrorMessage.
        /// </summary>
        public static async Task<UpdateCheckResult> CheckForUpdatesAsync(string currentVersion)
        {
            var result = new UpdateCheckResult { CurrentVersion = currentVersion };

            try
            {
                string json = await FetchReleasesJsonAsync().ConfigureAwait(false);

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                {
                    result.Success = false;
                    result.ErrorMessage = "No releases were found on GitHub for MBBSLauncher.";
                    return result;
                }

                // The API returns releases newest-first, so element [0] is the most recently
                // published release (pre-release or not). Skip any drafts just in case.
                JsonElement? newest = null;
                foreach (JsonElement release in doc.RootElement.EnumerateArray())
                {
                    if (release.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True)
                        continue;
                    newest = release;
                    break;
                }

                if (newest == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "No published releases were found on GitHub for MBBSLauncher.";
                    return result;
                }

                JsonElement rel = newest.Value;
                result.LatestVersion = GetString(rel, "tag_name");
                result.ReleaseTitle = GetString(rel, "name");
                result.DownloadUrl = GetString(rel, "html_url");
                result.IsPrerelease = rel.TryGetProperty("prerelease", out var pre) && pre.ValueKind == JsonValueKind.True;

                if (string.IsNullOrWhiteSpace(result.LatestVersion))
                {
                    result.Success = false;
                    result.ErrorMessage = "The latest release on GitHub did not include a version tag.";
                    return result;
                }

                int comparison = CompareVersions(currentVersion, result.LatestVersion);
                result.UpdateAvailable = comparison < 0;
                result.RunningNewerThanPublished = comparison > 0;
                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                // Manual check must never crash the app — surface a friendly message and log details.
                Program.LogError("UpdateChecker.CheckForUpdatesAsync", ex);
                result.Success = false;
                result.ErrorMessage =
                    "Could not reach GitHub to check for updates.\n\n" +
                    "Please verify this machine has internet access and try again.\n\n" +
                    "Details: " + ex.Message;
                return result;
            }
        }

        private static async Task<string> FetchReleasesJsonAsync()
        {
            using var client = new HttpClient { Timeout = RequestTimeout };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            using var cts = new CancellationTokenSource(RequestTimeout);
            using HttpResponseMessage response =
                await client.GetAsync(ReleasesApiUrl, cts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
        }

        private static string GetString(JsonElement element, string property)
        {
            return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? ""
                : "";
        }

        // ----------------------------------------------------------------------------------------
        // Version comparison
        //
        // System.Version cannot parse the project's tags ("v2.0-beta11", "v1.85"), so we roll a
        // small comparer. A version is modeled as a list of numeric parts plus a "release rank":
        //   - a stable release ranks ABOVE any pre-release of the same numeric version
        //     (SemVer rule: 2.0 > 2.0-beta11)
        //   - among pre-releases, the beta number breaks the tie (beta12 > beta11)
        // Returns <0 if a is older than b, 0 if equal, >0 if a is newer than b.
        // ----------------------------------------------------------------------------------------
        public static int CompareVersions(string a, string b)
        {
            var (partsA, rankA) = ParseVersion(a);
            var (partsB, rankB) = ParseVersion(b);

            int len = Math.Max(partsA.Length, partsB.Length);
            for (int i = 0; i < len; i++)
            {
                int va = i < partsA.Length ? partsA[i] : 0;
                int vb = i < partsB.Length ? partsB[i] : 0;
                if (va != vb)
                    return va < vb ? -1 : 1;
            }

            if (rankA != rankB)
                return rankA < rankB ? -1 : 1;

            return 0;
        }

        /// <summary>
        /// Extracts the numeric components and a pre-release rank from a tag string. Tolerant of the
        /// various historical tag shapes ("v1.85", "v2.0-beta11", "MBBS-Launcher-v1-00").
        /// Rank: long.MaxValue for a stable release (sorts highest); the beta number for a beta;
        /// 0 for an unnumbered pre-release word.
        /// </summary>
        private static (int[] parts, long rank) ParseVersion(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return (Array.Empty<int>(), long.MaxValue);

            string s = raw.Trim();

            // Grab the first dotted-numeric run (e.g. "2.0" out of "v2.0-beta11").
            Match numMatch = Regex.Match(s, @"\d+(?:\.\d+)*");
            int[] parts = Array.Empty<int>();
            if (numMatch.Success)
            {
                string[] pieces = numMatch.Value.Split('.');
                parts = new int[pieces.Length];
                for (int i = 0; i < pieces.Length; i++)
                    parts[i] = int.TryParse(pieces[i], out int n) ? n : 0;
            }

            long rank = long.MaxValue; // assume stable release unless a pre-release marker is found

            Match betaMatch = Regex.Match(s, @"beta[.\-]?(\d+)", RegexOptions.IgnoreCase);
            if (betaMatch.Success)
            {
                rank = long.TryParse(betaMatch.Groups[1].Value, out long betaNum) ? betaNum : 0;
            }
            else if (Regex.IsMatch(s, @"alpha|beta|rc|pre", RegexOptions.IgnoreCase))
            {
                rank = 0; // some pre-release marker without a number
            }

            return (parts, rank);
        }
    }
}
