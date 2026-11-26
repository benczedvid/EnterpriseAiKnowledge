import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { AskKnowledgeResponse, KnowledgeSource } from '../../../../core/models/knowledge.models';
import { KnowledgeService } from '../../../../core/services/knowledge.service';

@Component({
  selector: 'app-knowledge-search',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
  ],
  templateUrl: './knowledge-search.component.html',
  styleUrl: './knowledge-search.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class KnowledgeSearchComponent {

  private static readonly constMinimumQuestionLength = 3;
  private readonly knowledgeService = inject(KnowledgeService);
  readonly result = signal<AskKnowledgeResponse | null>(null);
  readonly isLoading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  question = '';

  ask(): void {
    const normalizedQuestion = this.question.trim();

    if (normalizedQuestion.length < KnowledgeSearchComponent.constMinimumQuestionLength) {
      this.errorMessage.set('A kérdés legalább 3 karakter hosszú legyen.'
      );

      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.result.set(null);

    this.knowledgeService
      .ask({
        question: normalizedQuestion,
      })
      .pipe(
        finalize(() => this.isLoading.set(false))
      )
      .subscribe({
        next: response => {
          this.result.set(response);
        },
        error: error => {
          const detail = error?.error?.detail;

          this.errorMessage.set(
            typeof detail === 'string' && detail.length > 0
              ? detail
              : 'A tudásbázis lekérdezése nem sikerült.'
          );
        }
      });
  }

  similarityPercent(source: KnowledgeSource): number {
    return Math.round(source.similarity * 100);
  }

  trackSource(_index: number, source: KnowledgeSource): string {
    return source.chunkId;
  }
}