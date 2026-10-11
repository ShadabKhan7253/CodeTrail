using CodeTrail.Api.Models;
using CodeTrail.Api.Services;

namespace CodeTrail.Api.Services;

/// <summary>
/// Orchestrates the full analysis pipeline:
/// 1. Validate input
/// 2. Fetch commit + diff from GitHub
/// 3. Run static C# impact analysis
/// 4. Request AI explanation
/// 5. Build and return the response DTO
/// </summary>
public class AnalysisOrchestrator(
    GitHubService github,
    GeminiAiService ai)
{
    private const int MaxCandidateFiles = 35;   // focused candidate pool
    private const int MaxFileBytes      = 50_000; // skip huge files

    public async Task<AnalysisResponse> AnalyzeAsync(AnalysisRequest request, CancellationToken ct)
    {
        var warnings = new List<string>();

        // ── 1. Validate ──────────────────────────────────────────────────────
        var (owner, repo) = github.ParseAndValidateUrl(request.RepositoryUrl);
        github.ValidateSha(request.CommitSha);

        // ── 2. Fetch commit ──────────────────────────────────────────────────
        CommitInfo commit;
        try
        {
            commit = await github.GetCommitAsync(owner, repo, request.CommitSha.Trim(), ct);
        }
        catch (KeyNotFoundException)
        {
            throw new ArgumentException($"Commit '{request.CommitSha}' was not found in {owner}/{repo}. " +
                                        "Verify the SHA and that the repository is public.");
        }

        if (commit.Files.Count == 0)
        {
            warnings.Add("This commit has no changed files (it may be an empty commit or a merge without unique changes).");
        }

        // ── 3. Build ChangedFileDtos ─────────────────────────────────────────
        var changedFiles = commit.Files.Select(f => new ChangedFileDto
        {
            Path          = f.Path,
            PreviousPath  = f.PreviousPath,
            ChangeType    = MapChangeType(f.Status),
            ChangedSymbols = CSharpAnalyzer.ExtractChangedSymbolsFromPatch(f.Patch ?? ""),
            DiffExcerpt   = f.Patch
        }).ToList();

        var csharpChanged = commit.Files
            .Where(f => f.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
                        f.Status != "truncated")
            .ToList();

        List<RelatedFileDto> relatedFiles = [];
        GraphDto? graph = null;

        if (csharpChanged.Count > 0)
        {
            // ── 4. Fetch content for changed .cs files in parallel ────────────
            var changedContents = new System.Collections.Concurrent.ConcurrentDictionary<string, string>();
            await Task.WhenAll(csharpChanged.Select(async f =>
            {
                var content = await github.GetFileContentAsync(owner, repo, commit.Sha, f.Path, ct);
                if (content != null && content.Length <= MaxFileBytes)
                {
                    changedContents[f.Path] = content;
                }
            }));

            foreach (var f in csharpChanged)
            {
                if (changedContents.TryGetValue(f.Path, out var content))
                {
                    var cf = changedFiles.FirstOrDefault(x => x.Path == f.Path);
                    if (cf != null && cf.ChangedSymbols.Count == 0)
                    {
                        var (_, types, _) = CSharpAnalyzer.ExtractSymbols(content);
                        if (types.Count > 0)
                        {
                            cf.ChangedSymbols.AddRange(types);
                        }
                        else
                        {
                            var baseName = Path.GetFileNameWithoutExtension(f.Path);
                            if (!string.IsNullOrEmpty(baseName))
                                cf.ChangedSymbols.Add(baseName);
                        }
                    }
                }
            }

            // ── 5. Fetch repo tree + content of candidate files ───────────────
            var fullTree = await github.GetCSharpFileTreeAsync(owner, repo, commit.Sha, ct);

            var candidatePaths = RankCandidatePaths(fullTree, csharpChanged, changedFiles);

            if (fullTree.Count > candidatePaths.Count)
            {
                warnings.Add($"Repository has {fullTree.Count} C# files; analyzed top {candidatePaths.Count} candidate files based on type references, proximity, and tests.");
            }

            var candidateContents = new System.Collections.Concurrent.ConcurrentDictionary<string, string>();
            using var sem = new SemaphoreSlim(8);
            var tasks = candidatePaths.Select(async path =>
            {
                await sem.WaitAsync(ct);
                try
                {
                    var content = await github.GetFileContentAsync(owner, repo, commit.Sha, path, ct);
                    if (content != null && content.Length <= MaxFileBytes)
                        candidateContents[path] = content;
                }
                finally
                {
                    sem.Release();
                }
            });
            await Task.WhenAll(tasks);

            // ── 6. Static analysis ────────────────────────────────────────────
            var candidates = CSharpAnalyzer.FindCandidates(
                changedContents.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                candidateContents.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                commit.Files);

            relatedFiles = candidates.Take(25).Select(c => new RelatedFileDto
            {
                Path         = c.Path,
                Relationship = c.Relationship,
                Evidence     = c.Evidence,
                Priority     = c.Score >= 10 ? "high" : c.Score >= 5 ? "medium" : "low",
                Confidence   = Math.Min(c.Score / 15.0, 1.0)
            }).ToList();

            // ── 7. Build dependency graph ─────────────────────────────────────
            graph = BuildGraph(changedFiles, relatedFiles);
        }
        else
        {
            warnings.Add("No C# files were changed — impact analysis was skipped.");
        }

        // ── 8. AI report ──────────────────────────────────────────────────────
        var aiReport = await ai.GenerateReportAsync(commit, changedFiles, relatedFiles, request.GeminiApiKey, ct);

        return new AnalysisResponse
        {
            Repository   = $"{owner}/{repo}",
            CommitSha    = commit.Sha,
            CommitMessage = commit.Message,
            CommitAuthor  = commit.Author,
            CommitDate    = commit.Date,
            ChangedFiles  = changedFiles,
            RelatedFiles  = relatedFiles,
            Graph         = graph,
            AiReport      = aiReport,
            Warnings      = warnings
        };
    }

    // ── Candidate Ranking ─────────────────────────────────────────────────────

    private static List<string> RankCandidatePaths(
        List<string> fullTree,
        List<DiffFile> csharpChanged,
        List<ChangedFileDto> changedFiles)
    {
        var changedSet = new HashSet<string>(csharpChanged.Select(f => f.Path), StringComparer.OrdinalIgnoreCase);
        var changedBases = csharpChanged
            .Select(f => Path.GetFileNameWithoutExtension(f.Path))
            .Where(b => !string.IsNullOrEmpty(b))
            .ToList();

        var changedDirs = csharpChanged
            .Select(f => Path.GetDirectoryName(f.Path) ?? "")
            .Where(d => !string.IsNullOrEmpty(d))
            .ToList();

        var allSymbols = changedFiles
            .SelectMany(f => f.ChangedSymbols)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var scored = new List<(string path, int score)>();

        foreach (var path in fullTree)
        {
            if (changedSet.Contains(path)) continue;

            int score = 0;
            var fileName = Path.GetFileNameWithoutExtension(path);
            var dir = Path.GetDirectoryName(path) ?? "";

            // 1. Direct name match / similarity to changed files
            foreach (var cb in changedBases)
            {
                if (fileName.Contains(cb, StringComparison.OrdinalIgnoreCase) ||
                    cb.Contains(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    score += 15;
                    break;
                }
            }

            // 2. Contains any changed symbol in file name
            foreach (var sym in allSymbols)
            {
                if (fileName.Contains(sym, StringComparison.OrdinalIgnoreCase))
                {
                    score += 12;
                    break;
                }
            }

            // 3. Test files (high interest for impact analysis)
            if (path.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("Spec", StringComparison.OrdinalIgnoreCase))
            {
                score += 8;
            }

            // 4. Same directory or sibling directory proximity
            if (changedDirs.Any(cd => dir.Equals(cd, StringComparison.OrdinalIgnoreCase)))
            {
                score += 6;
            }
            else if (changedDirs.Any(cd => dir.StartsWith(cd, StringComparison.OrdinalIgnoreCase) || cd.StartsWith(dir, StringComparison.OrdinalIgnoreCase)))
            {
                score += 3;
            }

            // Base score for any C# file to guarantee candidate pool
            score += 1;

            scored.Add((path, score));
        }

        return scored
            .OrderByDescending(s => s.score)
            .Take(MaxCandidateFiles)
            .Select(s => s.path)
            .ToList();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string MapChangeType(string status) => status switch
    {
        "added"    => "added",
        "removed"  => "deleted",
        "renamed"  => "renamed",
        "modified" => "modified",
        _          => status
    };

    private static GraphDto BuildGraph(List<ChangedFileDto> changed, List<RelatedFileDto> related)
    {
        var nodes = new List<GraphNode>();
        var edges = new List<GraphEdge>();
        var addedNodes = new HashSet<string>();

        foreach (var f in changed)
        {
            var id = NodeId(f.Path);
            if (addedNodes.Add(id))
            {
                nodes.Add(new GraphNode(id, Path.GetFileName(f.Path), "changed"));
            }
        }

        foreach (var r in related)
        {
            var id = NodeId(r.Path);
            if (addedNodes.Add(id))
            {
                nodes.Add(new GraphNode(id, Path.GetFileName(r.Path),
                    r.Path.Contains("Test", StringComparison.OrdinalIgnoreCase) ? "test" : "related"));
            }

            // Connect to changed file
            var targetChanged = changed.FirstOrDefault(c =>
            {
                var baseName = Path.GetFileNameWithoutExtension(c.Path);
                return Path.GetFileName(r.Path).Contains(baseName, StringComparison.OrdinalIgnoreCase);
            }) ?? changed.FirstOrDefault();

            if (targetChanged != null)
            {
                edges.Add(new GraphEdge(NodeId(targetChanged.Path), id, r.Relationship));
            }
        }

        return new GraphDto { Nodes = nodes, Edges = edges };
    }

    private static string NodeId(string path) =>
        path.Replace('/', '_').Replace('\\', '_').Replace('.', '_');
}
