# CodeTrail 🔍

**AI-assisted Visual Change-Impact Explorer for C#/.NET repositories**

CodeTrail accepts a public GitHub repository URL and commit SHA, identifies all changed files, finds potentially related source files using static C# analysis, and uses Google Gemini AI to explain possible impacts and suggest targeted tests.

---

## Architecture

```
frontend/          Angular 22 SPA (http://localhost:4200)
backend/           ASP.NET Core 10 Web API (http://localhost:5000)
  ├── Controllers/    AnalysisController (POST /api/analysis, GET /api/health)
  ├── Services/
  │   ├── GitHubService.cs       – GitHub REST API client
  │   ├── CSharpAnalyzer.cs      – Regex-based static impact analysis
  │   ├── GeminiAiService.cs     – Google Gemini 2.0 Flash integration
  │   └── AnalysisOrchestrator.cs – Pipeline coordinator
  └── Models/         DTOs and internal models
```

---

## Quick Start

### Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | 10.x |
| Node.js | 18+ |
| npm | 10+ |

### 1. Set environment variables (backend)

```bash
export GEMINI_API_KEY="your-gemini-api-key-here"
# Optional — increases GitHub rate limit from 60 to 5000 req/hr:
export GITHUB_TOKEN="your-github-personal-access-token"
```

### 2. Start the backend

```bash
cd backend
dotnet run
# → Listening on http://localhost:5000
```

### 3. Start the frontend

```bash
cd frontend
npm install    # only needed first time
npm start
# → http://localhost:4200
```

---

## Required Environment Variables

| Variable | Required | Purpose |
|----------|----------|---------|
| `GEMINI_API_KEY` | Yes (for AI) | Google Generative AI API key |
| `GITHUB_TOKEN` | No | GitHub PAT to raise rate limit to 5000/hr |

**Never put these in the Angular frontend.** They live only in backend environment variables.

---

## API Reference

### `GET /api/health`
Returns `{ "status": "ok", "timestamp": "..." }`.

### `POST /api/analysis`

**Request:**
```json
{
  "repositoryUrl": "https://github.com/owner/repo",
  "commitSha": "full-or-partial-sha"
}
```

**Response:**
```json
{
  "repository": "owner/repo",
  "commitSha": "abc123...",
  "commitMessage": "...",
  "commitAuthor": "...",
  "commitDate": "...",
  "changedFiles": [
    {
      "path": "src/Foo.cs",
      "changeType": "modified",
      "changedSymbols": ["FooService"],
      "diffExcerpt": "..."
    }
  ],
  "relatedFiles": [
    {
      "path": "src/Bar.cs",
      "relationship": "Type reference",
      "evidence": ["References changed type 'FooService'"],
      "priority": "high",
      "confidence": 0.73
    }
  ],
  "graph": { "nodes": [...], "edges": [...] },
  "aiReport": {
    "summary": "...",
    "possibleImpacts": ["..."],
    "suggestedTests": ["..."],
    "limitations": ["..."],
    "isAiFallback": false
  },
  "warnings": []
}
```

---

## Demo Commit

A reliable demo for a live presentation:

```
Repository URL: https://github.com/ardalis/CleanArchitecture
Commit SHA:     94839929c4a71d2062bd6a6dc144e0d38d4d88f8
```

This commit modifies a C# test file, demonstrating changed-symbol extraction, related-file candidates, and AI explanation.

---

## Static Analysis Approach

CodeTrail uses **regex-based** C# analysis (no Roslyn required):

1. **Symbol extraction** — extracts `namespace`, `class`, `interface`, `record`, `struct`, `enum` names from changed files.
2. **Reference scanning** — searches the bounded repo file tree for files that:
   - Import changed namespaces via `using` directives
   - Reference changed type names at word boundaries
   - Inherit from or implement changed types (via inheritance syntax scanning)
   - Have similar filenames to changed files
   - Are test files (xUnit, NUnit, MSTest markers) that reference changed code
3. **Evidence ranking** — each candidate is scored by evidence strength and ranked high/medium/low.

**Limits:** Up to 500 files in the tree scan, 60 files downloaded for analysis, 40 KB per file, 4 KB patch excerpt.

---

## Security Notes

- Only accepts HTTPS `github.com` URLs — rejects arbitrary hosts.
- Never executes repository code, build scripts, or tests.
- API keys are backend-only, never sent to Angular.
- All repository content is treated as untrusted text data.
- AI output is validated server-side before display; hallucinated file paths are not surfaced.
- Diff excerpts and file contents are rendered as text, not HTML.

---

## Known Limitations

- **Unauthenticated GitHub API**: 60 requests/hour without `GITHUB_TOKEN`.
- **Large repos**: Analysis is capped at 60 files. Set `GITHUB_TOKEN` for better coverage.
- **Non-.cs changes**: Impact analysis only runs when .cs files are changed.
- **Static analysis is heuristic** — it finds likely candidates, not guaranteed impacts.
- **AI is hypothesis-only** — the model may be wrong; evidence from static analysis is the ground truth.
- **No runtime analysis** — class instantiation, DI, and dynamic dispatch are not captured.
- **Merge commits** with no unique diff produce no results (a warning is shown).

---

## Two-Minute Demo Script

1. Open `http://localhost:4200`.
2. Paste `https://github.com/ardalis/CleanArchitecture` and `94839929c4a71d2062bd6a6dc144e0d38d4d88f8`.
3. Click **Analyze Commit** — show the real changed-file list with diff excerpt.
4. Switch to **Related Files** — expand a card and show the static evidence bullets.
5. Switch to **AI Report** — show the impact summary and suggested tests, noting "these are AI hypotheses, not confirmed facts."
6. Switch to **Graph** — explain the visual: red = changed, blue = related, green = test.
7. Point out the warning banner: "This is a review aid, not a complete dependency guarantee."

---

## License

MIT — see `LICENSE` file.

---

## AI Model Attribution

- **Model**: `gemini-2.0-flash` (Google Gemini)  
- **Provider**: Google  
- **License**: Proprietary / Google AI Terms of Service  
- **Use**: Hypothesis generation only; output is clearly labelled as AI-generated.
