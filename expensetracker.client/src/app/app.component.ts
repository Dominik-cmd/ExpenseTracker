import { CommonModule, DOCUMENT, NgClass } from '@angular/common';
import { Component, HostListener, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ToastModule } from 'primeng/toast';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { filter } from 'rxjs';

import { AuthService } from './core/services/auth.service';

const DARK_MODE_KEY = 'expense-tracker.dark-mode';
const SIDEBAR_KEY = 'expense-tracker.sidebar-collapsed';
const SIDEBAR_SECTIONS_KEY = 'expense-tracker.sidebar-sections';

interface NavItem {
  label: string;
  route: string;
  icon: string;
  adminOnly?: boolean;
}

interface NavGroup {
  label: string;
  items: NavItem[];
  collapsible: boolean;
  adminOnly?: boolean;
}

const NAV_GROUPS: NavGroup[] = [
  {
    label: 'Primary',
    collapsible: false,
    items: [
      { label: 'Dashboard', route: '/dashboard', icon: 'pi pi-chart-bar' },
      { label: 'Transactions', route: '/transactions', icon: 'pi pi-wallet' },
      { label: 'Investments', route: '/investments', icon: 'pi pi-chart-line' }
    ]
  },
  {
    label: 'Reports',
    collapsible: true,
    items: [
      { label: 'Monthly', route: '/reports/monthly', icon: 'pi pi-chart-line' },
      { label: 'Yearly', route: '/reports/yearly', icon: 'pi pi-chart-bar' },
      { label: 'Insights', route: '/reports/insights', icon: 'pi pi-sparkles' }
    ]
  },
  {
    label: 'Configuration',
    collapsible: true,
    items: [
      { label: 'Categories', route: '/categories', icon: 'pi pi-folder-open' },
      { label: 'Merchant rules', route: '/merchant-rules', icon: 'pi pi-sitemap' },
      { label: 'Parse failures', route: '/parse-failures', icon: 'pi pi-exclamation-triangle' },
      { label: 'Processing queue', route: '/queue', icon: 'pi pi-server' },
      { label: 'LLM logs', route: '/settings/llm-logs', icon: 'pi pi-list' }
    ]
  },
  {
    label: 'Settings',
    collapsible: true,
    items: [
      { label: 'Account', route: '/settings/account', icon: 'pi pi-user' },
      { label: 'LLM', route: '/settings/llm', icon: 'pi pi-bolt' },
      { label: 'Webhook', route: '/settings/webhook', icon: 'pi pi-send' },
      { label: 'Investment providers', route: '/settings/investments', icon: 'pi pi-briefcase' },
      { label: 'User management', route: '/admin/users', icon: 'pi pi-users', adminOnly: true }
    ]
  }
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
  private readonly document = inject(DOCUMENT);
  private readonly router = inject(Router);

  protected readonly navGroups = computed(() => {
    const isAdmin = this.authService.isAdmin();
    return NAV_GROUPS
      .filter(group => !group.adminOnly || isAdmin)
      .map(group => ({
        ...group,
        items: group.items.filter(item => !item.adminOnly || isAdmin)
      }))
      .filter(group => group.items.length > 0);
  });
  protected readonly darkMode = signal(this.readDarkModePreference());
  protected readonly sidebarCollapsed = signal(this.readSidebarPreference());
  protected readonly mobileSidebarOpen = signal(false);
  protected readonly expandedSections = signal<Set<string>>(this.readSectionPreferences());
  protected readonly isAuthenticated = this.authService.authenticated;
  protected readonly username = computed(() => this.authService.getUsername() ?? 'Expense user');
  protected readonly isMobile = signal(this.checkMobile());

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

    effect(() => {
      const sections = this.expandedSections();
      localStorage.setItem(SIDEBAR_SECTIONS_KEY, JSON.stringify([...sections]));
    });

    // Close mobile sidebar on navigation
    this.router.events.pipe(filter(e => e instanceof NavigationEnd)).subscribe(() => {
      if (this.isMobile()) {
        this.mobileSidebarOpen.set(false);
      }
    });
  }

  @HostListener('window:resize')
  onResize(): void {
    this.isMobile.set(this.checkMobile());
    if (!this.isMobile()) {
      this.mobileSidebarOpen.set(false);
    }
  }

  protected toggleSidebar(): void {
    if (this.isMobile()) {
      this.mobileSidebarOpen.update(v => !v);
    } else {
      this.sidebarCollapsed.update((v) => !v);
    }
  }

  protected closeMobileSidebar(): void {
    this.mobileSidebarOpen.set(false);
  }

  protected toggleSection(label: string): void {
    this.expandedSections.update(set => {
      const newSet = new Set(set);
      if (newSet.has(label)) {
        newSet.delete(label);
      } else {
        newSet.add(label);
      }
      return newSet;
    });
  }

  protected isSectionExpanded(group: NavGroup): boolean {
    if (!group.collapsible || this.sidebarCollapsed()) {
      return true;
    }

    return this.expandedSections().has(group.label);
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

  private checkMobile(): boolean {
    return typeof window !== 'undefined' && window.innerWidth < 768;
  }

  private readSectionPreferences(): Set<string> {
    try {
      const stored = localStorage.getItem(SIDEBAR_SECTIONS_KEY);
      if (stored) {
        return new Set(JSON.parse(stored));
      }
    } catch {
      return new Set<string>();
    }

    return new Set<string>();
  }
}

