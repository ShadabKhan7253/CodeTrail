namespace CodeTrail.Api.Models;

/// <summary>Internal representation of a GitHub commit diff.</summary>
public class CommitInfo
{
    public string Sha { get; set; } = "";
    public string Message { get; set; } = "";
    public string Author { get; set; } = "";
    public string Date { get; set; } = "";
    public List<DiffFile> Files { get; set; } = [];
    public string Owner { get; set; } = "";
    public string Repo { get; set; } = "";
}

public class DiffFile
{
    public string Path { get; set; } = "";
    public string? PreviousPath { get; set; }
    public string Status { get; set; } = "";   // added | modified | removed | renamed
    public int Additions { get; set; }
    public int Deletions { get; set; }
    public string? Patch { get; set; }          // raw diff excerpt, may be truncated
}

/// <summary>Internal result of static C# analysis.</summary>
public class AnalysisCandidate
{
    public string Path { get; set; } = "";
    public string Relationship { get; set; } = "";
    public List<string> Evidence { get; set; } = [];
    public int Score { get; set; }
}
