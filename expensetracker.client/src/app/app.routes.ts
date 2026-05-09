import { Routes } from '@angular/router';

import { authGuard } from './core/guards/auth.guard';
import { adminGuard } from './core/guards/admin.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  {
    path: 'login',
    loadComponent: () => import('./pages/login/login.component').then((m) => m.LoginComponent)
  },
  {
    path: 'dashboard',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/dashboard/dashboard.component').then((m) => m.DashboardComponent)
  },
  {
    path: 'transactions',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/transactions/transactions.component').then((m) => m.TransactionsComponent)
  },
  {
    path: 'categories',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/categories/categories.component').then((m) => m.CategoriesComponent)
  },
  {
    path: 'rules',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/merchant-rules/merchant-rules.component').then((m) => m.MerchantRulesComponent)
  },
  { path: 'merchant-rules', pathMatch: 'full', redirectTo: 'rules' },
  {
    path: 'parse-failures',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/parse-failures/parse-failures.component').then((m) => m.ParseFailuresComponent)
  },
  {
    path: 'reports/monthly',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/reports/monthly/monthly-report.component').then((m) => m.MonthlyReportComponent)
  },
  {
    path: 'reports/yearly',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/reports/yearly/yearly-report.component').then((m) => m.YearlyReportComponent)
  },
  {
    path: 'reports/insights',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/reports/insights/insights.component').then((m) => m.InsightsComponent)
  },
  {
    path: 'settings/llm-logs',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/settings/llm-logs/llm-logs.component').then((m) => m.LlmLogsComponent)
  },
  {
    path: 'settings/llm',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/settings/llm/llm-settings.component').then((m) => m.LlmSettingsComponent)
  },
  {
    path: 'settings/account',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/settings/account/account-settings.component').then((m) => m.AccountSettingsComponent)
  },
  {
    path: 'settings/webhook',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/settings/webhook/webhook-settings.component').then((m) => m.WebhookSettingsComponent)
  },
  {
    path: 'admin/users',
    canActivate: [adminGuard],
    loadComponent: () => import('./pages/admin/user-management.component').then((m) => m.UserManagementComponent)
  },
  { path: '**', redirectTo: 'dashboard' }
];
