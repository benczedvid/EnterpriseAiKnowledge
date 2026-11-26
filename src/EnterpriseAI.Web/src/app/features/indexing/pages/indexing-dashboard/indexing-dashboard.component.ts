import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { EMPTY, Subject, catchError, finalize, forkJoin, switchMap, takeUntil, takeWhile, tap, timer } from 'rxjs';
import { IndexingRun, IndexingRunDetails, IndexingSummary, StartIndexingBatchRequest } from '../../../../core/models/indexing.models';
import { IndexingService } from '../../../../core/services/indexing.service';

@Component({
  selector: 'app-indexing-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    DatePipe,
    DecimalPipe
  ],
  templateUrl: './indexing-dashboard.component.html',
  styleUrl: './indexing-dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class IndexingDashboardComponent implements OnInit, OnDestroy {
  private readonly indexingService = inject(IndexingService);

  readonly summary = signal<IndexingSummary | null>(null);
  readonly runs = signal<IndexingRun[]>([]);
  readonly selectedRun = signal<IndexingRunDetails | null>(null);

  readonly isLoading = signal(false);
  readonly isStartingBatch = signal(false);
  readonly isLoadingRun = signal(false);

  readonly errorMessage = signal<string | null>(null);
  readonly batchMessage = signal<string | null>(null);

  private static readonly activeRunStorageKey = 'enterprise-ai-active-indexing-run-id';

  private readonly destroy$ = new Subject<void>();

  readonly activeRunId = signal<string | null>(null);
  readonly activeRun = signal<IndexingRunDetails | null>(null);


  readonly hasErrors = computed(() => {
    const currentSummary = this.summary();

    return (currentSummary?.documents.failed ?? 0) > 0 || (currentSummary?.latestRun?.failedCount ?? 0) > 0;
  });

  readonly indexedPercentage = computed(() => {
    const documents = this.summary()?.documents;

    if (!documents || documents.total === 0) {
      return 0;
    }

    return Math.round(
      (documents.indexed / documents.total) * 100
    );
  });

  maximumDocuments = 500;
  folderPath = '';
  forceReindex = false;

  ngOnInit(): void {
    this.loadDashboard();

    const storedRunId = localStorage.getItem(IndexingDashboardComponent.activeRunStorageKey
    );

    if (storedRunId) {
      this.watchRun(storedRunId);
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadDashboard(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    forkJoin({
      summary: this.indexingService.getSummary(),
      runs: this.indexingService.getLatestRuns(10)
    })
      .pipe(
        finalize(() => this.isLoading.set(false))
      )
      .subscribe({
        next: ({ summary, runs }) => {
          this.summary.set(summary);
          this.runs.set(runs.runs);
        },
        error: () => {
          this.errorMessage.set('Az indexelési adatok betöltése nem sikerült.');
        }
      });
  }

  selectRun(run: IndexingRun): void {
    if (this.selectedRun()?.id === run.id) {
      this.selectedRun.set(null);
      return;
    }

    this.isLoadingRun.set(true);
    this.errorMessage.set(null);

    this.indexingService
      .getRunById(run.id)
      .pipe(
        finalize(() => this.isLoadingRun.set(false))
      )
      .subscribe({
        next: details => {
          this.selectedRun.set(details);
        },
        error: () => {
          this.errorMessage.set('Az indexelési futás részleteinek betöltése nem sikerült.');
        }
      });
  }

  startBatch(): void {
    const maximumDocuments = Number(this.maximumDocuments);

    if (
      !Number.isInteger(maximumDocuments)
      || maximumDocuments < 1
      || maximumDocuments > 500
    ) {
      this.errorMessage.set('A dokumentumok száma 1 és 500 közötti egész szám legyen.');

      return;
    }

    const request: StartIndexingBatchRequest = {
      folderPath: this.normalizeFolderPath(this.folderPath),
      maximumDocuments,
      forceReindex: this.forceReindex
    };

    this.isStartingBatch.set(true);
    this.errorMessage.set(null);
    this.batchMessage.set(null);

    this.indexingService
      .startBatch(request)
      .pipe(
        finalize(() =>
          this.isStartingBatch.set(false)
        )
      )
      .subscribe({
        next: response => {
          localStorage.setItem(
            IndexingDashboardComponent.activeRunStorageKey,
            response.indexingRunId
          );

          this.activeRunId.set(
            response.indexingRunId
          );

          this.batchMessage.set(
            'Az indexelés elindult a háttérben. Az oldal bezárása vagy újratöltése nem szakítja meg.'
          );

          this.watchRun(response.indexingRunId);

          this.loadDashboard();
        },
        error: error => {
          const detail = error?.error?.detail;

          this.errorMessage.set(
            typeof detail === 'string'
              && detail.length > 0
              ? detail
              : 'A SharePoint indexelés indítása nem sikerült.'
          );
        }
      });
  }

  statusLabel(status: string): string {
    switch (status) {
      case 'Running':
        return 'Folyamatban';
      case 'Completed':
        return 'Befejezve';
      case 'CompletedWithErrors':
        return 'Befejezve hibákkal';
      case 'Failed':
        return 'Sikertelen';
      case 'Cancelled':
        return 'Megszakítva';
      case 'Indexed':
        return 'Indexelve';
      case 'SkippedUnchanged':
        return 'Változatlan';
      case 'SkippedUnsupported':
        return 'Nem támogatott';
      case 'RequiresOcr':
        return 'OCR szükséges';
      case 'NotFound':
        return 'Nem található';
      default:
        return status;
    }
  }

  statusClass(status: string): string {
    switch (status) {
      case 'Completed':
      case 'Indexed':
        return 'status-success';

      case 'Running':
        return 'status-running';

      case 'CompletedWithErrors':
      case 'RequiresOcr':
        return 'status-warning';

      case 'Failed':
      case 'NotFound':
        return 'status-error';

      default:
        return 'status-neutral';
    }
  }

  trackRun(_index: number, run: IndexingRun): string {
    return run.id;
  }

  trackRunItem(_index: number, item: { id: string }): string {
    return item.id;
  }

  private normalizeFolderPath(folderPath: string): string | null {
    const normalized = folderPath.trim();

    return normalized.length === 0
      ? null
      : normalized;
  }
  private watchRun(indexingRunId: string): void {
    this.activeRunId.set(indexingRunId);

    timer(0, 3000)
      .pipe(
        switchMap(() =>
          this.indexingService
            .getRunById(indexingRunId)
            .pipe(
              catchError(error => {
                if (error?.status === 404) {
                  localStorage.removeItem(IndexingDashboardComponent.activeRunStorageKey);

                  this.activeRunId.set(null);
                }

                this.errorMessage.set('Az indexelési futás állapotának lekérése nem sikerült.');

                return EMPTY;
              })
            )
        ),
        tap(run => {
          this.activeRun.set(run);
          this.selectedRun.set(run);
        }),
        takeWhile(
          run => run.status === 'Running',
          true
        ),
        takeUntil(this.destroy$)
      )
      .subscribe({
        complete: () => {
          const run = this.activeRun();

          if (run && run.status !== 'Running') {
            localStorage.removeItem(IndexingDashboardComponent.activeRunStorageKey);

            this.activeRunId.set(null);

            this.batchMessage.set(this.createCompletionMessage(run));

            this.loadDashboard();
          }
        }
      });
  }

  private createCompletionMessage(run: IndexingRunDetails): string {
    switch (run.status) {
      case 'Completed':
        return (
          'Az indexelés befejeződött. ' +
          `${run.indexedCount} indexelve, ` +
          `${run.skippedUnchangedCount} változatlan.`
        );

      case 'CompletedWithErrors':
        return (
          'Az indexelés hibákkal fejeződött be. '
          + `${run.indexedCount} indexelve, `
          + `${run.failedCount} hibás.`
        );

      case 'Cancelled':
        return ('Az indexelés az API leállítása miatt megszakadt.'
        );

      case 'Failed':
        return (
          'Az indexelési futás sikertelenül fejeződött be.'
        );

      default:
        return `Az indexelés állapota: ${run.status}.`;
    }
  }
}