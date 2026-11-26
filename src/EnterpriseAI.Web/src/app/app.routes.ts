import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'knowledge'
  },
  {
    path: 'knowledge',
    loadComponent: () =>
      import(
        './features/knowledge/pages/knowledge-search/'
        + 'knowledge-search.component'
      ).then(component => component.KnowledgeSearchComponent)
  },
  {
    path: 'indexing',
    loadComponent: () =>
      import(
        './features/indexing/pages/indexing-dashboard/'
        + 'indexing-dashboard.component'
      ).then(component => component.IndexingDashboardComponent)
  },
  {
    path: '**',
    redirectTo: 'knowledge'
  }
];