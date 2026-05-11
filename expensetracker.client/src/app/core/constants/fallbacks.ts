import {
  Category,
  DashboardAnalytics,
  DashboardStrip,
  InsightsReport,
  MonthlyReport,
  PagedResult,
  QueueStatus,
  WebhookSettings,
  YearlyReport,
  LlmSettings,
  AccountSettings,
  MerchantRule,
  RawMessage
} from '../models';

export const EMPTY_PAGED_RESULT = <T>(): PagedResult<T> => ({
  items: [],
  totalCount: 0,
  page: 1,
  pageSize: 10
});

export const EMPTY_CATEGORIES: Category[] = [];

export const EMPTY_MERCHANT_RULES: MerchantRule[] = [];

export const EMPTY_RAW_MESSAGES: RawMessage[] = [];

export const EMPTY_DASHBOARD_STRIP: DashboardStrip = {
  monthToDate: 0,
  onPace: 0,
  netLast30: 0,
  netLast30Income: 0,
  netLast30Spending: 0
};

export const EMPTY_DASHBOARD_ANALYTICS: DashboardAnalytics = {
  categoryLeaderboard: [],
  recentTransactions: [],
  income: null
};

export const EMPTY_MONTHLY_REPORT: MonthlyReport = {
  year: new Date().getFullYear(),
  month: new Date().getMonth() + 1,
  totalAmount: 0,
  previousMonthTotal: 0,
  percentChange: 0,
  rolling3MonthAverage: 0,
  categoryComparisons: [],
  dailySpending: [],
  topMerchants: [],
  dailyCategoryBreakdown: []
};

export const EMPTY_YEARLY_REPORT: YearlyReport = {
  year: new Date().getFullYear(),
  yearTotal: 0,
  previousYearTotal: 0,
  percentChange: 0,
  monthlyCategories: [],
  largestTransactions: [],
  categoryEvolution: [],
  calendarHeatmap: []
};

export const EMPTY_INSIGHTS_REPORT: InsightsReport = {
  calendarHeatmap: [],
  dayOfWeekAverages: [],
  recurringTransactions: [],
  subscriptionAnomalies: [],
  firstTimeMerchants: [],
  quietDays: []
};

export const EMPTY_QUEUE_STATUS: QueueStatus = {
  pendingCount: 0,
  pending: [],
  recentlyProcessed: []
};

export const EMPTY_WEBHOOK_SETTINGS: WebhookSettings = {
  secret: '',
  senders: []
};

export const EMPTY_LLM_SETTINGS: LlmSettings = {
  providers: [],
  activeProvider: null
};

export const EMPTY_ACCOUNT_SETTINGS: AccountSettings = {
  username: '',
  defaultCurrency: 'EUR',
  emailNotifications: true,
  webhookNotifications: false
};
