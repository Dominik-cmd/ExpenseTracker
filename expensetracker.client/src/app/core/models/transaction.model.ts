export interface Transaction {
  id: string;
  userId: string;
  amount: number;
  currency: string;
  direction: string;
  transactionType: string;
  transactionDate: string;
  merchantRaw?: string | null;
  merchantNormalized?: string | null;
  categoryId: string;
  categorySource: string;
  transactionSource: string;
  notes?: string | null;
  isDeleted: boolean;
  rawMessageId?: string | null;
  createdAt: string;
  updatedAt: string;
  categoryName: string;
  parentCategoryName?: string | null;
}

export interface TransactionFilters {
  from?: string;
  to?: string;
  categoryId?: string;
  categoryIds?: string[];
  merchant?: string;
  minAmount?: number;
  maxAmount?: number;
  direction?: string;
  source?: string;
  page?: number;
  pageSize?: number;
}

export interface CreateTransactionRequest {
  amount: number;
  direction: string;
  transactionType: string;
  transactionDate: string;
  merchantRaw?: string | null;
  categoryId: string;
  notes?: string | null;
}

export interface UpdateTransactionRequest {
  amount?: number | null;
  currency?: string | null;
  direction?: string | null;
  transactionType?: string | null;
  transactionDate?: string | null;
  merchantRaw?: string | null;
  categoryId?: string | null;
  notes?: string | null;
}

export interface RecategorizeTransactionRequest {
  categoryId: string;
  createMerchantRule: boolean;
}

export interface BulkRecategorizeRequest {
  transactionIds: string[];
  categoryId: string;
  createMerchantRule: boolean;
}
