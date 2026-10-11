namespace CodeTrail.Api.Models;

// ── Request ──────────────────────────────────────────────────────────────────

public record AnalysisRequest(string RepositoryUrl, string CommitSha, string? GeminiApiKey = null);

// ── Response ─────────────────────────────────────────────────────────────────

public record AnalysisResponse
{
    public string Repository { get; init; } = "";
    public string CommitSha { get; init; } = "";
    public string CommitMessage { get; init; } = "";
    public string CommitAuthor { get; init; } = "";
    public string CommitDate { get; init; } = "";
    public List<ChangedFileDto> ChangedFiles { get; init; } = [];
    public List<RelatedFileDto> RelatedFiles { get; init; } = [];
    public GraphDto? Graph { get; init; }
    public AiReportDto? AiReport { get; init; }
    public List<string> Warnings { get; init; } = [];
}

public record ChangedFileDto
{
    public string Path { get; init; } = "";
    public string ChangeType { get; init; } = "";   // added | modified | deleted | renamed
    public string? PreviousPath { get; init; }
    public List<string> ChangedSymbols { get; init; } = [];
    public string? DiffExcerpt { get; init; }
}

public record RelatedFileDto
{
    public string Path { get; init; } = "";
    public string Relationship { get; init; } = "";   // e.g. "Implements interface"
    public List<string> Evidence { get; init; } = [];
    public string Priority { get; init; } = "low";   // high | medium | low
    public double Confidence { get; init; }
}

public record GraphDto
{
    public List<GraphNode> Nodes { get; init; } = [];
    public List<GraphEdge> Edges { get; init; } = [];
}

public record GraphNode(string Id, string Label, string Kind);   // kind: changed | related | test
public record GraphEdge(string Source, string Target, string Label);

public record AiReportDto
{
    public string Summary { get; init; } = "";
    public List<string> PossibleImpacts { get; init; } = [];
    public List<string> SuggestedTests { get; init; } = [];
    public List<string> Limitations { get; init; } = [];
    public bool IsAiFallback { get; init; }
}
