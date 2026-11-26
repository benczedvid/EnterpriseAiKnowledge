import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

interface NavigationItem {
  path: string;
  label: string;
  description: string;
}

@Component({
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  selector: 'app-root',
  templateUrl: './app.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class App {
  readonly navigationItems: readonly NavigationItem[] = [
    {
      path: '/knowledge',
      label: 'Tudáskereső',
      description: 'Kérdések a dokumentumok alapján'
    },
    {
      path: '/indexing',
      label: 'Indexelés',
      description: 'SharePoint indexelési dashboard'
    }
  ];

  readonly isMenuOpen = signal(false);

  toggleMenu(): void {
    this.isMenuOpen.update(isOpen => !isOpen);
  }

  closeMenu(): void {
    this.isMenuOpen.set(false);
  }
}
