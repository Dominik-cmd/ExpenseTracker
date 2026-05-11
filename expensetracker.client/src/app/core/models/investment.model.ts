export interface InvestmentDashboardStrip {
  totalValue: number;
  dayChange: number | null;
  dayChangePercent: number | null;
  ytdChange: number | null;
  ytdChangePercent: number | null;
  hasData: boolean;
}

export interface PortfolioSummary {
  totalValue: number;
  ibkrValue: number;
  manualValue: number;
  dayChange: number | null;
  dayChangePercent: number | null;
  ytdChange: number | null;
  ytdChangePercent: number | null;
  baseCurrency: string;
  asOf: string;
  oldestManualUpdateDays: number | null;
}

export interface AccountSummary {
  accountId: string;
  displayName: string;
  accountType: string;
  providerType: string;
  icon: string;
  color: string;
  value: number;
  currency: string;
  valueInBaseCurrency: number;
  lastUpdated: string | null;
  daysSinceUpdate: number | null;
}

export interface InvestmentHolding {
  id: string;
  accountName: string;
  symbol: string;
  name: string | null;
  assetClass: string;
  quantity: number;
  costBasisPerShare: number | null;
  markPrice: number | null;
  marketValue: number;
  unrealizedPnl: number | null;
  unrealizedPnlPercent: number | null;
  currency: string;
}

export interface AllocationBreakdown {
  allocationType: string;
  totalValue: number;
  slices: AllocationSlice[];
}

export interface AllocationSlice {
  label: string;
  value: number;
  percentage: number;
}

export interface HistoryPoint {
  date: string;
  value: number;
}

export interface RecentActivity {
  date: string;
  accountId: string;
  accountDisplayName: string;
  providerType: string;
  activityType: string;
  description: string;
  amount: number | null;
  currency: string;
  quantity: number | null;
  instrumentSymbol: string | null;
}

export interface ManualAccount {
  id: string;
  displayName: string;
  accountType: string;
  currency: string;
  balance: number | null;
  icon: string | null;
  color: string | null;
  notes: string | null;
  isActive: boolean;
  lastUpdated: string | null;
}

export interface InvestmentProvider {
  id: string;
  providerType: string;
  displayName: string;
  token: string | null;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  extraConfig: any;
  isEnabled: boolean;
  lastSyncAt: string | null;
  lastSyncStatus: string | null;
  lastSyncError: string | null;
  lastTestAt: string | null;
  lastTestStatus: string | null;
  lastTestError: string | null;
}
