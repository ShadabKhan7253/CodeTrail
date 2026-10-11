using System.Text.RegularExpressions;
using CodeTrail.Api.Models;

namespace CodeTrail.Api.Services;

/// <summary>
/// Lightweight static C# impact analyzer.
/// No Roslyn required — uses regex-based type extraction.
/// </summary>
public static class CSharpAnalyzer
{
    // ── Patterns ─────────────────────────────────────────────────────────────

    private static readonly Regex NamespaceRx   = new(@"namespace\s+([\w.]+)", RegexOptions.Compiled);
    private static readonly Regex ClassRx        = new(@"\b(?:class|interface|record|struct|enum)\s+(\w+)", RegexOptions.Compiled);
    private static readonly Regex UsingRx        = new(@"^using\s+([\w.]+)\s*;", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex InheritsRx     = new(@"\b(?:class|record)\s+\w+\s*[:<]([^{]+)", RegexOptions.Compiled);

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts the type names and namespace declared in a C# source snippet.
    /// </summary>
    public static (string @namespace, List<string> types, List<string> usings) ExtractSymbols(string source)
    {
        var ns = NamespaceRx.Match(source).Groups[1].Value;
        var types  = ClassRx.Matches(source).Select(m => m.Groups[1].Value).Distinct().ToList();
        var usings = UsingRx.Matches(source).Select(m => m.Groups[1].Value).Distinct().ToList();
        return (ns, types, usings);
    }

    /// <summary>
    /// Given the set of symbols (types, namespaces) from changed files, scans
    /// candidate file contents and returns ranked impact candidates.
    /// </summary>
    public static List<AnalysisCandidate> FindCandidates(
        Dictionary<string, string> changedFileContents,        // path → content
        Dictionary<string, string> candidateFileContents,      // path → content
        List<DiffFile> changedFiles)
    {
        // 1. Build symbol index from changed files
        var changedSymbols = new HashSet<string>(StringComparer.Ordinal);
        var changedNamespaces = new HashSet<string>(StringComparer.Ordinal);
        var changedPaths = new HashSet<string>(changedFiles.Select(f => f.Path), StringComparer.OrdinalIgnoreCase);

        foreach (var (_, src) in changedFileContents)
        {
            var (ns, types, _) = ExtractSymbols(src);
            if (!string.IsNullOrEmpty(ns)) changedNamespaces.Add(ns);
            foreach (var t in types) changedSymbols.Add(t);
        }

        var results = new List<AnalysisCandidate>();

        foreach (var (path, src) in candidateFileContents)
        {
            if (changedPaths.Contains(path)) continue;   // skip already-changed files

            var evidence = new List<string>();
            int score = 0;

            var (ns, types, usings) = ExtractSymbols(src);

            // (a) Namespace import match
            foreach (var u in usings)
            {
                if (changedNamespaces.Any(cn => u.Equals(cn, StringComparison.OrdinalIgnoreCase) ||
                                                u.StartsWith(cn + ".", StringComparison.OrdinalIgnoreCase)))
                {
                    evidence.Add($"Imports namespace '{u}' which contains changed types");
                    score += 3;
                    break;
                }
            }

            // (b) Direct type reference
            foreach (var sym in changedSymbols)
            {
                if (string.IsNullOrWhiteSpace(sym)) continue;
                if (ContainsWordBoundary(src, sym))
                {
                    evidence.Add($"References changed type '{sym}'");
                    score += 5;
                }
            }

            // (c) Inheritance / implementation
            var inheritMatches = InheritsRx.Matches(src);
            foreach (Match m in inheritMatches)
            {
                var baseTypes = m.Groups[1].Value.Split(',')
                    .Select(s => s.Trim().Split('<')[0].Trim());  // strip generics
                foreach (var bt in baseTypes)
                {
                    if (changedSymbols.Contains(bt))
                    {
                        evidence.Add($"Inherits from or implements changed type '{bt}'");
                        score += 8;
                    }
                }
            }

            // (d) Test file heuristic
            bool isTest = path.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
                          path.Contains("Spec",  StringComparison.OrdinalIgnoreCase) ||
                          src.Contains("[Test]")  || src.Contains("[Fact]") ||
                          src.Contains("[Theory]") || src.Contains("NUnit") ||
                          src.Contains("xunit");
            if (isTest && score > 0)
            {
                evidence.Add("Is a test file — likely exercises the changed code");
                score += 4;
            }

            // (e) Filename similarity
            foreach (var changed in changedFiles)
            {
                var changedBase = Path.GetFileNameWithoutExtension(changed.Path);
                var candidateBase = Path.GetFileNameWithoutExtension(path);
                if (!string.IsNullOrEmpty(changedBase) &&
                    candidateBase.Contains(changedBase, StringComparison.OrdinalIgnoreCase))
                {
                    evidence.Add($"Filename is similar to changed file '{Path.GetFileName(changed.Path)}'");
                    score += 2;
                }
            }

            if (evidence.Count > 0)
            {
                results.Add(new AnalysisCandidate
                {
                    Path = path,
                    Relationship = DeriveRelationship(evidence),
                    Evidence = evidence,
                    Score = score
                });
            }
        }

        return [.. results.OrderByDescending(c => c.Score)];
    }

    /// <summary>Extracts type names visible in a patch/diff.</summary>
    public static List<string> ExtractChangedSymbolsFromPatch(string patch)
    {
        if (string.IsNullOrEmpty(patch)) return [];
        return ClassRx.Matches(patch)
                       .Select(m => m.Groups[1].Value)
                       .Distinct()
                       .ToList();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool ContainsWordBoundary(string source, string word)
    {
        // Simple word-boundary check without Regex overhead
        int idx = source.IndexOf(word, StringComparison.Ordinal);
        while (idx >= 0)
        {
            bool leftOk  = idx == 0 || !char.IsLetterOrDigit(source[idx - 1]) && source[idx - 1] != '_';
            bool rightOk = idx + word.Length >= source.Length ||
                           !char.IsLetterOrDigit(source[idx + word.Length]) && source[idx + word.Length] != '_';
            if (leftOk && rightOk) return true;
            idx = source.IndexOf(word, idx + 1, StringComparison.Ordinal);
        }
        return false;
    }

    private static string DeriveRelationship(List<string> evidence)
    {
        if (evidence.Any(e => e.Contains("Inherits") || e.Contains("implements")))
            return "Inheritance / Implementation";
        if (evidence.Any(e => e.Contains("test", StringComparison.OrdinalIgnoreCase)))
            return "Test coverage";
        if (evidence.Any(e => e.Contains("References")))
            return "Type reference";
        return "Namespace / filename proximity";
    }
}
