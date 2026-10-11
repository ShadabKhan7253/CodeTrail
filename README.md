# CodeTrail 🔍

> **AI-Assisted Visual Change-Impact Explorer for C#/.NET Repositories**

CodeTrail helps developers and reviewers instantly understand the blast radius of any commit in a C# repository. Given a public GitHub repository URL and commit SHA, CodeTrail identifies changed files, extracts code symbols, traces dependent and affected source files using static analysis, and uses Google Gemini AI to formulate impact hypotheses and suggest targeted tests.

---

## 📑 Table of Contents
- [Architecture & Tech Stack](#-architecture--tech-stack)
- [Prerequisites](#-prerequisites)
- [Step-by-Step Setup Guide](#-step-by-step-setup-guide)
  - [1. Clone Repository](#1-clone-repository)
  - [2. Configure API Keys](#2-configure-api-keys)
  - [3. Start Backend (.NET Web API)](#3-start-backend-net-web-api)
  - [4. Start Frontend (Angular SPA)](#4-start-frontend-angular-spa)
- [Testing & Demoing CodeTrail](#-testing--demoing-codetrail)
  - [Recommended Demo Commit](#recommended-demo-commit)
  - [Alternative Public Repo Demo](#alternative-public-repo-demo)
- [Static Analysis & AI Pipeline](#-static-analysis--ai-pipeline)
- [API Reference](#-api-reference)
- [Security & Rate Limiting](#-security--rate-limiting)
- [Troubleshooting & FAQs](#-troubleshooting--faqs)

---

## 🏗 Architecture & Tech Stack

```
CodeTrail/
├── frontend/               # Angular 22 Single Page Application (http://localhost:4200)
│   ├── src/app/
│   │   ├── components/     # Analysis form, File diff, Graph viewer, AI Report card
│   │   ├── services/       # AnalysisService (HTTP client to backend)
│   │   └── models/         # TypeScript analysis contracts
└── backend/                # ASP.NET Core 10 Web API (http://localhost:5050)
    ├── Controllers/        # AnalysisController (POST /api/analysis, GET /api/health)
    ├── Services/
    │   ├── GitHubService.cs        # GitHub REST client with raw content fallback
    │   ├── CSharpAnalyzer.cs       # Static symbol & reference extraction
    │   ├── GeminiAiService.cs      # Google Gemini 3.8 Flash AI reasoning
    │   └── AnalysisOrchestrator.cs # Parallelized pipeline coordinator
    ├── Models/             # C# DTOs and analysis graph models
    ├── appsettings.json    # Base configuration
    └── appsettings.Local.json # Untracked local secret configuration
```

| Component | Technology | Default URL / Port |
|-----------|------------|---------------------|
| **Backend API** | ASP.NET Core 10, C# | `http://localhost:5050` |
| **Frontend UI** | Angular 22, TypeScript, Vanilla CSS | `http://localhost:4200` |
| **AI Engine** | Google Gemini `gemini-3.8-flash` | REST API via Google AI Studio |
| **Code Source** | GitHub REST API + raw content endpoints | api.github.com |

---

## ⚙️ Prerequisites

Before getting started, make sure you have the following installed on your system:

| Tool | Minimum Version | Installation Link |
|------|-----------------|-------------------|
| **.NET SDK** | .NET 10.x | [Download .NET SDK](https://dotnet.microsoft.com/download) |
| **Node.js** | 18.x or 20.x+ | [Download Node.js](https://nodejs.org/) |
| **npm** | 9.x+ | Bundled with Node.js |
| **Git** | 2.x+ | [Download Git](https://git-scm.com/) |

---

## 🚀 Step-by-Step Setup Guide

### 1. Clone Repository

```bash
git clone https://github.com/ShadabKhan7253/CodeTrail.git
cd CodeTrail
```

---

### 2. Configure API Keys

CodeTrail requires a **Google Gemini API Key** for AI impact analysis. A **GitHub Token** is optional, but recommended to avoid unauthenticated GitHub rate limits (60 requests/hr vs. 5,000/hr).

> [!TIP]
> **Get a free Gemini API key**: [Google AI Studio](https://aistudio.google.com/app/apikey).

You can configure your keys using either of the two methods below:

#### Method A: Local JSON File (Recommended for Local Dev)
Create a file named `appsettings.Local.json` in the `backend/` directory (this file is already in `.gitignore` and won't be pushed):

```bash
# In CodeTrail/backend/appsettings.Local.json
{
  "GEMINI_API_KEY": "your_gemini_api_key_here",
  "GITHUB_TOKEN": "your_optional_github_token_here"
}
```

#### Method B: Environment Variables
Alternatively, export the keys in your shell:

```bash
# macOS / Linux
export GEMINI_API_KEY="your_gemini_api_key_here"
export GITHUB_TOKEN="your_optional_github_token_here"

# Windows (Command Prompt)
set GEMINI_API_KEY=your_gemini_api_key_here
set GITHUB_TOKEN=your_optional_github_token_here

# Windows (PowerShell)
$env:GEMINI_API_KEY="your_gemini_api_key_here"
$env:GITHUB_TOKEN="your_optional_github_token_here"
```

---

### 3. Start Backend (.NET Web API)

The backend runs on **port 5050** to avoid conflicts with macOS ControlCenter / AirPlay Receiver:

```bash
cd backend
dotnet run
```

You should see:
```text
Building...
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5050
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

**Verify Backend Health**:
Open `http://localhost:5050/api/health` in your browser or curl:
```bash
curl http://localhost:5050/api/health
# Output: {"status":"healthy","service":"CodeTrail.Api"}
```

---

### 4. Start Frontend (Angular SPA)

In a new terminal window:

```bash
cd frontend
npm install
npm start
```

The Angular dev server will compile and start at `http://localhost:4200`.

Open your browser to:
👉 **[http://localhost:4200](http://localhost:4200)**

---

## 🧪 Testing & Demoing CodeTrail

### Recommended Demo Commit

We prepared a dedicated sample C# e-commerce repository with real services, domain models, and unit tests:

| Field | Value |
|-------|-------|
| **Repository URL** | `https://github.com/ShadabKhan7253/CodeTrailSampleApp` |
| **Commit SHA** | `10e1fb8` |

#### Steps to Test:
1. Open `http://localhost:4200`.
2. Enter the Repository URL: `https://github.com/ShadabKhan7253/CodeTrailSampleApp`.
3. Enter Commit SHA: `10e1fb8`.
4. Click **Analyze Commit**.
5. Observe:
   - **Changed Files**: `BookDiscountService.cs` (adds a 15% VIP discount rule).
   - **Static Impact Analysis**: Identifies 5 affected files (`OrderProcessingService.cs`, `CheckoutWorkflow.cs`, `DiscountEngineTests.cs`, etc.) with high/medium confidence based on type references and namespace imports.
   - **Interactive Graph**: Visual interactive dependency graph (Red = changed file, Blue = dependent/related file, Green = unit tests).
   - **Gemini AI Report**: Hypothesis summarizing business impact, risk factors, and recommended unit/integration tests.

---

### Alternative Public Repo Demo

You can also run CodeTrail against popular public C# open-source projects:

| Field | Value | Description |
|-------|-------|-------------|
| **Repository URL** | `https://github.com/ardalis/CleanArchitecture` | Popular C# Clean Architecture template |
| **Commit SHA** | `94839929c4a71d2062bd6a6dc144e0d38d4d88f8` | C# test modification |

---

## 🔍 Static Analysis & AI Pipeline

CodeTrail operates entirely via remote repository inspection — **no local cloning or code execution required**:

1. **GitHub Commit Ingestion**:
   - Fetches commit metadata, changed files, and unified git diffs via GitHub REST API.
2. **Regex-Based Symbol Extraction**:
   - Parses declared namespaces, classes, interfaces, records, structs, and methods inside changed files.
3. **Candidate Tree & Reference Tracing**:
   - Inspects the repository file tree.
   - Downloads candidate `.cs` files concurrently.
   - Evaluates syntactic type references, `using` namespace directives, interface implementations, and naming proximity.
4. **Impact Scoring**:
   - Computes relationship scores (`high`, `medium`, `low`) with concrete evidence markers.
5. **Gemini 3.8 Flash AI Synthesis**:
   - Passes the diff and affected symbol graph to Google Gemini.
   - Formulates explainable hypotheses: why the code changed, potential ripple effects, edge cases, and targeted tests.

---

## 📡 API Reference

### Health Check
```http
GET /api/health
```
**Response:**
```json
{
  "status": "healthy",
  "service": "CodeTrail.Api"
}
```

### Run Impact Analysis
```http
POST /api/analysis
Content-Type: application/json
```
**Request Body:**
```json
{
  "repositoryUrl": "https://github.com/owner/repository",
  "commitSha": "abc1234",
  "geminiApiKey": "optional-override-key",
  "gitHubToken": "optional-override-token"
}
```

**Response Format:**
```json
{
  "repository": "owner/repository",
  "commitSha": "abc1234...",
  "commitMessage": "feat: add VIP discount logic",
  "author": "developer",
  "date": "2026-10-11T...",
  "changedFiles": [
    {
      "path": "Services/BookDiscountService.cs",
      "status": "modified",
      "additions": 12,
      "deletions": 2,
      "diffExcerpt": "@@ ... @@",
      "extractedSymbols": ["BookDiscountService", "ApplyVipDiscount"]
    }
  ],
  "relatedFiles": [
    {
      "path": "Services/OrderProcessingService.cs",
      "relationship": "Type reference",
      "confidence": 0.85,
      "evidence": ["References changed type 'BookDiscountService'"]
    }
  ],
  "aiReport": {
    "summary": "This commit adds a 15% VIP discount check...",
    "possibleImpacts": ["Existing orders might calculate lower totals..."],
    "suggestedTests": ["Verify order checkout with VIP flag enabled and disabled"]
  }
}
```

---

## 🔒 Security & Rate Limiting

- **No Code Execution**: CodeTrail never builds or executes arbitrary code from analyzed repositories.
- **Strict HTTPS Validation**: Only standard `https://github.com/{owner}/{repo}` repositories are accepted.
- **Secret Isolation**: Secrets in `appsettings.Local.json` are excluded from source control.
- **Rate Limits**:
  - Unauthenticated GitHub API calls allow 60 requests/hour.
  - Adding a free GitHub Personal Access Token (`GITHUB_TOKEN`) increases the limit to 5,000 requests/hour.

---

## ❓ Troubleshooting & FAQs

### Q: Port 5050 or 5000 is already in use
**A:** On macOS Monterey/Ventura/Sonoma, port 5000 is often reserved by Apple's AirPlay Receiver. CodeTrail uses **port 5050** by default. To kill an existing process on port 5050:
```bash
# Find PID
lsof -i :5050
# Kill process
kill -9 <PID>
```

### Q: "Push cannot contain secrets" on GitHub
**A:** Never commit your actual API keys into `backend/appsettings.json`. Always put your keys into `backend/appsettings.Local.json` or your terminal's environment variables.

### Q: How do I stop the servers?
**A:** Press `Ctrl + C` in the respective terminal windows where `dotnet run` or `npm start` are executing.

---

## 📄 License
MIT License. Built for developers, teams, and reviewers who want visual clarity on code changes.
