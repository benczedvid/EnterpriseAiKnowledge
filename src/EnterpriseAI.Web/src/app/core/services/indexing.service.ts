import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import {
  IndexingRunDetails,
  IndexingRunsResponse,
  IndexingSummary,
  StartIndexingBatchRequest,
  StartIndexingBatchResponse
} from '../models/indexing.models';

@Injectable({
  providedIn: 'root'
})
export class IndexingService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  getSummary(): Observable<IndexingSummary> {
    return this.http.get<IndexingSummary>(`${this.baseUrl}/indexing/summary`
    );
  }

  getLatestRuns(limit = 10): Observable<IndexingRunsResponse> {
    const params = new HttpParams().set('limit', limit.toString());

    return this.http.get<IndexingRunsResponse>(`${this.baseUrl}/indexing-runs`, { params });
  }

  getRunById(indexingRunId: string): Observable<IndexingRunDetails> {
    return this.http.get<IndexingRunDetails>(`${this.baseUrl}/indexing-runs/${encodeURIComponent(indexingRunId)}`);
  }

  startBatch(request: StartIndexingBatchRequest): Observable<StartIndexingBatchResponse> {
    return this.http.post<StartIndexingBatchResponse>(`${this.baseUrl}/dev/sharepoint/index-batch`, request);
  }
}