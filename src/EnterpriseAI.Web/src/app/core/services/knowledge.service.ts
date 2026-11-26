import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import {
  AskKnowledgeRequest,
  AskKnowledgeResponse
} from '../models/knowledge.models';

@Injectable({
  providedIn: 'root'
})
export class KnowledgeService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  ask(
    request: AskKnowledgeRequest
  ): Observable<AskKnowledgeResponse> {
    return this.http.post<AskKnowledgeResponse>(
      `${this.baseUrl}/knowledge/ask`,
      request
    );
  }
}