using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CodeTrail.Api.Models;

namespace CodeTrail.Api.Services;

/// <summary>
/// Fetches commit metadata and changed files from the GitHub REST API.
/// Uses unauthenticated access or GITHUB_TOKEN if set.
/// </summary>
public class GitHubService(HttpClient http, IConfiguration config, ILogger<GitHubService> logger)
{
    private const int MaxFiles = 80;
    private const int MaxPatchBytes = 4_000;

    private static readonly Regex RepoUrlRegex =
        new(@"^https://github\.com/(?<owner>[A-Za-z0-9_.\-]+)/(?<repo>[A-Za-z0-9_.\-]+?)(?:\.git)?/?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ShaRegex =
        new(@"^[0-9a-fA-F]{7,40}$", RegexOptions.Compiled);

    // ── Validation ───────────────────────────────────────────────────────────

    public (string owner, string repo) ParseAndValidateUrl(string url)
    {
        var m = RepoUrlRegex.Match(url.Trim());
        if (!m.Success)
            throw new ArgumentException("URL must be an HTTPS github.com repository URL.");
        return (m.Groups["owner"].Value, m.Groups["repo"].Value);
    }

    public void ValidateSha(string sha)
    {
        if (!ShaRegex.IsMatch(sha.Trim()))
            throw new ArgumentException("Commit SHA must be 7–40 hex characters.");
    }

    // ── HTTP helper ──────────────────────────────────────────────────────────

    private void SetupRequest(HttpRequestMessage req)
    {
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        req.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        req.Headers.UserAgent.ParseAdd("CodeTrail/1.0");
        var token = config["GITHUB_TOKEN"];
        if (!string.IsNullOrWhiteSpace(token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        SetupRequest(req);
        using var resp = await http.SendAsync(req, ct);

        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new KeyNotFoundException($"GitHub returned 404 for: {url}");
        if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden ||
            resp.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            throw new InvalidOperationException("GitHub API rate limit reached. Try again later.");
        if ((int)resp.StatusCode == 422)
            throw new ArgumentException("The commit SHA is ambiguous or invalid in this repository. Please provide a longer or full-length SHA (40 characters).");

        resp.EnsureSuccessStatusCode();
        var stream = await resp.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public async Task<CommitInfo> GetCommitAsync(string owner, string repo, string sha, CancellationToken ct)
    {
        logger.LogInformation("Fetching commit {Sha} from {Owner}/{Repo}", sha, owner, repo);

        var url = $"https://api.github.com/repos/{owner}/{repo}/commits/{sha}";
        using var doc = await GetJsonAsync(url, ct);

        var root = doc.RootElement;
        var commit = root.GetProperty("commit");

        var info = new CommitInfo
        {
            Sha = root.GetProperty("sha").GetString() ?? sha,
            Owner = owner,
            Repo = repo,
            Message = commit.TryGetProperty("message", out var msg) ? msg.GetString() ?? "" : "",
            Author = commit.TryGetProperty("author", out var auth) &&
                     auth.TryGetProperty("name", out var name)
                         ? name.GetString() ?? "" : "",
            Date = commit.TryGetProperty("author", out var auth2) &&
                   auth2.TryGetProperty("date", out var date)
                       ? date.GetString() ?? "" : "",
        };

        if (!root.TryGetProperty("files", out var filesEl))
            return info;

        int count = 0;
        foreach (var f in filesEl.EnumerateArray())
        {
            if (++count > MaxFiles)
            {
                info.Files.Add(new DiffFile
                {
                    Path = $"… ({filesEl.GetArrayLength() - MaxFiles} more files not shown)",
                    Status = "truncated"
                });
                break;
            }

            var patch = f.TryGetProperty("patch", out var p) ? p.GetString() : null;
            if (patch != null && patch.Length > MaxPatchBytes)
                patch = patch[..MaxPatchBytes] + "\n… (patch truncated)";

            info.Files.Add(new DiffFile
            {
                Path = f.TryGetProperty("filename", out var fn) ? fn.GetString() ?? "" : "",
                PreviousPath = f.TryGetProperty("previous_filename", out var pf) ? pf.GetString() : null,
                Status = f.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "",
                Additions = f.TryGetProperty("additions", out var a) ? a.GetInt32() : 0,
                Deletions = f.TryGetProperty("deletions", out var d) ? d.GetInt32() : 0,
                Patch = patch
            });
        }

        return info;
    }

    /// <summary>Fetches raw file content. Prefers raw.githubusercontent.com to avoid GitHub API rate limits.</summary>
    public async Task<string?> GetFileContentAsync(string owner, string repo, string sha, string path, CancellationToken ct)
    {
        try
        {
            // 1. Try raw.githubusercontent.com first (unlimited rate limit for public repositories)
            var rawUrl = $"https://raw.githubusercontent.com/{owner}/{repo}/{sha}/{path}";
            using var rawReq = new HttpRequestMessage(HttpMethod.Get, rawUrl);
            rawReq.Headers.UserAgent.ParseAdd("CodeTrail/1.0");
            using var rawResp = await http.SendAsync(rawReq, ct);
            if (rawResp.IsSuccessStatusCode)
            {
                return await rawResp.Content.ReadAsStringAsync(ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug("raw.githubusercontent.com failed for {Path}: {Msg}", path, ex.Message);
        }

        try
        {
            // 2. Fallback to GitHub REST Contents API
            var url = $"https://api.github.com/repos/{owner}/{repo}/contents/{Uri.EscapeDataString(path)}?ref={sha}";
            using var doc = await GetJsonAsync(url, ct);

            if (doc.RootElement.TryGetProperty("content", out var contentEl))
            {
                var b64 = contentEl.GetString() ?? "";
                b64 = b64.Replace("\n", "").Replace("\r", "");
                return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(b64));
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug("Contents API failed for {Path}: {Msg}", path, ex.Message);
        }
        return null;
    }

    /// <summary>Returns a list of C# file paths in the repository tree.</summary>
    public async Task<List<string>> GetCSharpFileTreeAsync(string owner, string repo, string sha, CancellationToken ct)
    {
        try
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}/git/trees/{sha}?recursive=1";
            using var doc = await GetJsonAsync(url, ct);

            var files = new List<string>();
            if (!doc.RootElement.TryGetProperty("tree", out var tree)) return files;

            foreach (var item in tree.EnumerateArray())
            {
                if (!item.TryGetProperty("type", out var type) || type.GetString() != "blob") continue;
                if (!item.TryGetProperty("path", out var pathEl)) continue;

                var path = pathEl.GetString() ?? "";
                if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;
                if (IsExcluded(path)) continue;

                files.Add(path);
                if (files.Count >= 500) break;   // cap to avoid huge repos
            }

            return files;
        }
        catch (Exception ex)
        {
            logger.LogWarning("Could not fetch file tree: {Msg}", ex.Message);
            return [];
        }
    }

    private static bool IsExcluded(string path)
    {
        var lower = path.ToLowerInvariant();
        var excluded = new[] { ".git/", "/bin/", "/obj/", "/node_modules/", "/.vs/", "/generated/", "/vendor/" };
        return excluded.Any(e => lower.Contains(e));
    }
}
