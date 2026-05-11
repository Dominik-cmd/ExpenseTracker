export interface RawMessage {
  id: string;
  sender: string;
  body: string;
  receivedAt: string;
  parseStatus: string;
  errorMessage?: string | null;
  idempotencyHash: string;
  transactionId?: string | null;
  createdAt: string;
}

export interface ParsedSmsPreview {
  direction: string;
  transactionType: string;
  amount: number;
  currency: string;
  transactionDate: string;
  merchantRaw: string;
  merchantNormalized: string;
  notes?: string | null;
}

export interface ManualParseResult {
  success: boolean;
  parsedSms: ParsedSmsPreview | null;
  errorMessage?: string | null;
}
