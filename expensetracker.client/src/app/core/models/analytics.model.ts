import { Transaction } from './transaction.model';

export interface DashboardKpi {
  currentMonth: number;
  last30Days: number;
  prev30Days: number;
  percentChange: number;
  projectedMonthEnd: number;
  sameMonthLastYear?: number | null;
  netFlow30d: number;
  income30d: number;
  spending30d: number;
}

export interface CategoryBreakdown {
  name: string;
  color?: string | null;
  amount: number;
  percentage: number;
}

export interface DailySpending {
  date: string;
  amount: number;
  rolling7Day?: number | null;
  rolling30Day?: number | null;
}

export interface TopMerchant {
  name: string;
  totalAmount: number;
  transactionCount: number;
}

export interface YtdWidget {
  total: number;
  avgPerDay: number;
  projectedEoy: number;
  yoyDelta?: number | null;
  topCategories: CategoryBreakdown[];
}

export interface IncomeWidget {
  currentMonth: number;
  last30Days: number;
  ytdTotal: number;
  netLast30Days: number;
  sources: IncomeSource[];
  monthlyComparison: MonthlyIncomeVsSpending[];
}

export interface IncomeSource {
  name: string;
  totalAmount: number;
  transactionCount: number;
}

export interface MonthlyIncomeVsSpending {
  year: number;
  month: number;
  label: string;
  income: number;
  spending: number;
  net: number;
}

export interface DashboardAnalytics {
  categoryLeaderboard: CategoryBreakdown[];
  recentTransactions: Transaction[];
  income: IncomeWidget | null;
}

export interface DashboardStrip {
  monthToDate: number;
  onPace: number;
  netLast30: number;
  netLast30Income: number;
  netLast30Spending: number;
}

export interface CategoryComparison {
  name: string;
  currentAmount: number;
  previousAmount: number;
  deltaAmount: number;
  deltaPercent: number;
}

export interface DailyCategorySpending {
  date: string;
  categoryName: string;
  color: string | null;
  amount: number;
}

export interface MonthlyReport {
  year: number;
  month: number;
  totalAmount: number;
  previousMonthTotal: number;
  percentChange: number;
  rolling3MonthAverage: number;
  categoryComparisons: CategoryComparison[];
  dailySpending: DailySpending[];
  topMerchants: TopMerchant[];
  dailyCategoryBreakdown: DailyCategorySpending[];
}

export interface MonthlyCategoryTotal {
  month: number;
  categoryName: string;
  amount: number;
}

export interface MonthlyValue {
  month: number;
  amount: number;
}

export interface MonthlyCategorySeries {
  categoryName: string;
  color?: string | null;
  values: MonthlyValue[];
}

export interface YearlyReport {
  year: number;
  yearTotal: number;
  previousYearTotal: number;
  percentChange: number;
  monthlyCategories: MonthlyCategoryTotal[];
  largestTransactions: Transaction[];
  categoryEvolution: MonthlyCategorySeries[];
  calendarHeatmap: CalendarHeatmapPoint[];
}

export interface CalendarHeatmapPoint {
  date: string;
  amount: number;
}

export interface DayOfWeekAverage {
  dayOfWeek: string;
  averageAmount: number;
  transactionCount: number;
}

export interface RecurringTransactionInsight {
  merchant: string;
  averageAmount: number;
  latestAmount: number;
  latestDate: string;
  isAnomaly: boolean;
  cadence: string;
  occurrenceCount: number;
  deviationPercent: number;
}

export interface FirstTimeMerchantInsight {
  merchant: string;
  amount: number;
  transactionDate: string;
}

export interface InsightsReport {
  calendarHeatmap: CalendarHeatmapPoint[];
  dayOfWeekAverages: DayOfWeekAverage[];
  recurringTransactions: RecurringTransactionInsight[];
  subscriptionAnomalies: RecurringTransactionInsight[];
  firstTimeMerchants: FirstTimeMerchantInsight[];
  quietDays: string[];
}
