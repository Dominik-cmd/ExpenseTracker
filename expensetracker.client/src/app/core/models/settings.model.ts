export interface LlmProvider {
  id: string;
  providerType: string;
  name: string;
  model: string;
  isEnabled: boolean;
  apiKey: string | null;
  lastTestedAt?: string | null;
  lastTestStatus?: string | null;
}

export interface UpdateLlmProviderRequest {
  model?: string | null;
  apiKey?: string | null;
  isEnabled?: boolean;
}

export interface LlmTestResult {
  success: boolean;
  latencyMs: number;
  errorMessage?: string | null;
}

export interface LlmSettings {
  providers: LlmProvider[];
  activeProvider: LlmProvider | null;
}

export interface WebhookSettings {
  secret: string;
  senders: string[];
}

export interface AccountSettings {
  username: string;
  defaultCurrency: string;
  emailNotifications: boolean;
  webhookNotifications: boolean;
}

export interface LlmLogEntry {
  id: string;
  providerType: string;
  model: string;
  merchantRaw: string | null;
  merchantNormalized: string | null;
  amount: number | null;
  systemPrompt: string;
  userPrompt: string;
  responseRaw: string | null;
  parsedCategory: string | null;
  parsedSubcategory: string | null;
  parsedConfidence: number | null;
  parsedReasoning: string | null;
  latencyMs: number;
  success: boolean;
  errorMessage: string | null;
  createdAt: string;
}
