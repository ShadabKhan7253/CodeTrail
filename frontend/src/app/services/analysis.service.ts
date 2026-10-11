import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AnalysisRequest, AnalysisResponse } from '../models/analysis.models';

@Injectable({ providedIn: 'root' })
export class AnalysisService {
  private readonly apiBase = 'http://localhost:5000/api';

  constructor(private http: HttpClient) {}

  analyze(request: AnalysisRequest): Observable<AnalysisResponse> {
    return this.http
      .post<AnalysisResponse>(`${this.apiBase}/analysis`, request)
      .pipe(catchError(this.handleError));
  }

  health(): Observable<unknown> {
    return this.http.get(`${this.apiBase}/health`);
  }

  private handleError(err: HttpErrorResponse): Observable<never> {
    let message = 'An unexpected error occurred.';
    if (err.error?.error) {
      message = err.error.error;
    } else if (err.status === 0) {
      message = 'Cannot connect to the CodeTrail backend. Make sure it is running on port 5000.';
    } else if (err.status === 429) {
      message = 'GitHub API rate limit reached. Wait a few minutes and try again.';
    } else if (err.status === 404) {
      message = err.error?.error ?? 'Repository or commit not found.';
    } else if (err.status === 400) {
      message = err.error?.error ?? 'Invalid request.';
    }
    return throwError(() => new Error(message));
  }
}
