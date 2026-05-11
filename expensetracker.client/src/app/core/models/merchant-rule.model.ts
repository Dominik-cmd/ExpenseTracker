export interface MerchantRule {
  id: string;
  merchantNormalized: string;
  categoryId: string;
  categoryName: string;
  parentCategoryName?: string | null;
  createdBy: string;
  hitCount: number;
  lastHitAt?: string | null;
  createdAt: string;
}

export interface UpdateMerchantRuleRequest {
  categoryId: string;
  applyToExistingTransactions?: boolean;
}
