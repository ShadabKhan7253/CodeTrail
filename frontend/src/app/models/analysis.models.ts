// ── API Response Models ────────────────────────────────────────────────────

export interface AnalysisRequest {
  repositoryUrl: string;
  commitSha: string;
  geminiApiKey?: string;
}

export interface ChangedFile {
  path: string;
  changeType: string;       // added | modified | deleted | renamed
  previousPath?: string;
  changedSymbols: string[];
  diffExcerpt?: string;
}

export interface RelatedFile {
  path: string;
  relationship: string;
  evidence: string[];
  priority: 'high' | 'medium' | 'low';
  confidence: number;
}

export interface GraphNode {
  id: string;
  label: string;
  kind: 'changed' | 'related' | 'test';
}

export interface GraphEdge {
  source: string;
  target: string;
  label: string;
}

export interface Graph {
  nodes: GraphNode[];
  edges: GraphEdge[];
}

export interface AiReport {
  summary: string;
  possibleImpacts: string[];
  suggestedTests: string[];
  limitations: string[];
  isAiFallback: boolean;
}

export interface AnalysisResponse {
  repository: string;
  commitSha: string;
  commitMessage: string;
  commitAuthor: string;
  commitDate: string;
  changedFiles: ChangedFile[];
  relatedFiles: RelatedFile[];
  graph?: Graph;
  aiReport?: AiReport;
  warnings: string[];
}

export interface ApiError {
  error: string;
}
