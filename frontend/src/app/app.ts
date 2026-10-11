import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { AnalysisService } from './services/analysis.service';
import { FilterKindPipe } from './pipes/filter-kind.pipe';
import {
  AnalysisResponse, ChangedFile, RelatedFile
} from './models/analysis.models';

type ActiveTab = 'changed' | 'related' | 'ai' | 'graph';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule, FilterKindPipe],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App implements OnInit {
  // ── Form state ────────────────────────────────────────────────────────────
  repoUrl = 'https://github.com/ardalis/Result';
  commitSha = '774de7812d2e7394e63c22384153c552a6841ab6';
  geminiApiKey = '';
  showApiKeyInput = false;
  repoUrlError = '';
  shaError = '';

  demoPresets = [
    {
      title: 'Ardalis.Result',
      subtitle: 'Add BindAsync overload to ResultExtensions',
      repo: 'https://github.com/ardalis/Result',
      sha: '774de7812d2e7394e63c22384153c552a6841ab6'
    },
    {
      title: 'CleanArchitecture',
      subtitle: 'Functional Docker tests verification',
      repo: 'https://github.com/ardalis/CleanArchitecture',
      sha: '94839929c4a71d2062bd6a6dc144e0d38d4d88f8'
    }
  ];

  // ── Analysis state ────────────────────────────────────────────────────────
  loading = false;
  result: AnalysisResponse | null = null;
  error = '';
  activeTab: ActiveTab = 'changed';

  // ── File expansion ────────────────────────────────────────────────────────
  expandedChanged = new Set<number>();
  expandedRelated = new Set<number>();

  constructor(
    private analysisService: AnalysisService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    // Pre-filled with tested working demo
  }

  loadPreset(p: { repo: string; sha: string }): void {
    this.repoUrl = p.repo;
    this.commitSha = p.sha;
    this.repoUrlError = '';
    this.shaError = '';
    this.analyze();
  }

  toggleApiKeyInput(): void {
    this.showApiKeyInput = !this.showApiKeyInput;
    this.cdr.detectChanges();
  }

  // ── Validation ────────────────────────────────────────────────────────────

  validateForm(): boolean {
    this.repoUrlError = '';
    this.shaError = '';

    if (!this.repoUrl.trim()) {
      this.repoUrlError = 'Repository URL is required.';
    } else if (!/^https:\/\/github\.com\/[A-Za-z0-9_.\-]+\/[A-Za-z0-9_.\-]+\/?$/.test(this.repoUrl.trim())) {
      this.repoUrlError = 'Must be an HTTPS github.com URL (e.g. https://github.com/owner/repo).';
    }

    if (!this.commitSha.trim()) {
      this.shaError = 'Commit SHA is required.';
    } else if (!/^[0-9a-fA-F]{7,40}$/.test(this.commitSha.trim())) {
      this.shaError = 'Must be a valid hex SHA (7–40 characters).';
    }

    return !this.repoUrlError && !this.shaError;
  }

  // ── Analysis ──────────────────────────────────────────────────────────────

  analyze(): void {
    if (!this.validateForm()) return;

    this.loading = true;
    this.result = null;
    this.error = '';
    this.expandedChanged.clear();
    this.expandedRelated.clear();

    this.analysisService.analyze({
      repositoryUrl: this.repoUrl.trim(),
      commitSha: this.commitSha.trim(),
      geminiApiKey: this.geminiApiKey.trim() || undefined
    }).subscribe({
      next: (res) => {
        this.result = res;
        this.loading = false;
        this.activeTab = 'changed';
        this.cdr.detectChanges();
      },
      error: (err: Error) => {
        this.error = err.message;
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  reset(): void {
    this.result = null;
    this.error = '';
    this.repoUrlError = '';
    this.shaError = '';
    this.cdr.detectChanges();
  }

  // ── Tab ───────────────────────────────────────────────────────────────────

  setTab(tab: ActiveTab): void {
    this.activeTab = tab;
    this.cdr.detectChanges();
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  toggleChanged(i: number): void {
    if (this.expandedChanged.has(i)) this.expandedChanged.delete(i);
    else this.expandedChanged.add(i);
    this.cdr.detectChanges();
  }

  toggleRelated(i: number): void {
    if (this.expandedRelated.has(i)) this.expandedRelated.delete(i);
    else this.expandedRelated.add(i);
    this.cdr.detectChanges();
  }

  isChangedExpanded(i: number): boolean { return this.expandedChanged.has(i); }
  isRelatedExpanded(i: number): boolean { return this.expandedRelated.has(i); }

  getFileIcon(path: string): string {
    if (path.endsWith('.cs')) return '📄';
    if (path.endsWith('.json')) return '🔧';
    if (path.endsWith('.xml') || path.endsWith('.csproj') || path.endsWith('.sln')) return '📦';
    if (path.endsWith('.md')) return '📝';
    if (path.endsWith('.yml') || path.endsWith('.yaml')) return '⚙️';
    return '📁';
  }

  githubFileUrl(repoPath: string, filePath: string, sha: string): string {
    return `https://github.com/${repoPath}/blob/${sha}/${filePath}`;
  }

  githubCommitUrl(repoPath: string, sha: string): string {
    return `https://github.com/${repoPath}/commit/${sha}`;
  }

  formatDate(iso: string): string {
    if (!iso) return '';
    return new Date(iso).toLocaleDateString('en-US', {
      year: 'numeric', month: 'short', day: 'numeric',
      hour: '2-digit', minute: '2-digit'
    });
  }

  shortSha(sha: string): string { return sha?.slice(0, 7) ?? ''; }

  getPriorityColor(priority: string): string {
    if (priority === 'high') return '#f43f5e';
    if (priority === 'medium') return '#f59e0b';
    return '#22d3a6';
  }

  confidenceWidth(c: number): string { return `${Math.round(c * 100)}%`; }

  trackByIndex(i: number): number { return i; }

  get csharpChangedCount(): number {
    return this.result?.changedFiles.filter(f => f.path.endsWith('.cs')).length ?? 0;
  }

  get hasGraph(): boolean {
    return (this.result?.graph?.nodes?.length ?? 0) > 0;
  }

  get demoCommitMessage(): string {
    return this.result?.commitMessage?.split('\n')[0] ?? '';
  }
}
