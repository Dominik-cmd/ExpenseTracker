import { CommonModule, DOCUMENT, NgClass } from '@angular/common';
import { Component, computed, DestroyRef, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ToastModule } from 'primeng/toast';
import { ToggleSwitchModule } from 'primeng/toggleswitch';

import { AuthService } from './core/services/auth.service';

const DARK_MODE_KEY = 'expense-tracker.dark-mode';
const SIDEBAR_KEY = 'expense-tracker.sidebar-collapsed';

interface NavItem {
  label: string;
  route: string;
  icon: string;
  adminOnly?: boolean;
}

const NAV_ITEMS: NavItem[] = [
  { label: 'Dashboard', route: '/dashboard', icon: 'pi pi-home' },
  { label: 'Transactions', route: '/transactions', icon: 'pi pi-wallet' },
  { label: 'Categories', route: '/categories', icon: 'pi pi-folder-open' },
  { label: 'Merchant rules', route: '/merchant-rules', icon: 'pi pi-sitemap' },
  { label: 'Parse failures', route: '/parse-failures', icon: 'pi pi-exclamation-triangle' },
  { label: 'Monthly report', route: '/reports/monthly', icon: 'pi pi-chart-line' },
  { label: 'Yearly report', route: '/reports/yearly', icon: 'pi pi-chart-bar' },
  { label: 'Insights', route: '/reports/insights', icon: 'pi pi-sparkles' },
  { label: 'LLM logs', route: '/settings/llm-logs', icon: 'pi pi-list' },
  { label: 'LLM settings', route: '/settings/llm', icon: 'pi pi-bolt' },
  { label: 'Account settings', route: '/settings/account', icon: 'pi pi-user' },
  { label: 'Webhook settings', route: '/settings/webhook', icon: 'pi pi-send' },
  { label: 'User management', route: '/admin/users', icon: 'pi pi-users', adminOnly: true }
];

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NgClass,
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    ButtonModule,
    ConfirmDialogModule,
    ToastModule,
    ToggleSwitchModule
  ],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent {
  private readonly authService = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly document = inject(DOCUMENT);
  private readonly router = inject(Router);

  protected readonly navItems = computed(() => {
    const isAdmin = this.authService.isAdmin();
    return NAV_ITEMS.filter(item => !item.adminOnly || isAdmin);
  });
  protected readonly darkMode = signal(this.readDarkModePreference());
  protected readonly sidebarCollapsed = signal(this.readSidebarPreference());
  protected readonly isAuthenticated = this.authService.authenticated;
  protected readonly username = computed(() => this.authService.getUsername() ?? 'Expense user');

  constructor() {
    effect(() => {
      const enabled = this.darkMode();
      this.document.documentElement.classList.toggle('app-dark', enabled);
      this.document.body.classList.toggle('app-dark', enabled);
      localStorage.setItem(DARK_MODE_KEY, String(enabled));
    });

    effect(() => {
      localStorage.setItem(SIDEBAR_KEY, String(this.sidebarCollapsed()));
    });
  }

  protected toggleSidebar(): void {
    this.sidebarCollapsed.update((v) => !v);
  }

  protected logout(): void {
    this.authService.logout().subscribe();
  }

  private readDarkModePreference(): boolean {
    const storedValue = localStorage.getItem(DARK_MODE_KEY);
    if (storedValue !== null) {
      return storedValue === 'true';
    }

    return typeof window !== 'undefined' && window.matchMedia?.('(prefers-color-scheme: dark)').matches === true;
  }

  private readSidebarPreference(): boolean {
    const stored = localStorage.getItem(SIDEBAR_KEY);
    return stored === 'true'; // open by default (collapsed = false)
  }
}

