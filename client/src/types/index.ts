export interface PriceData {
  timestamp: number;
  price: number;
}

export interface Asset {
  id: string;
  name: string;
  ticker: string;
  icon: string;
}

export interface RiskAnalysisResponse {
  assetId: string;
  compositeRiskScore: number;
  volatilityScore: number;
  trendScore: number;
  volumeScore: number;

  // Advanced financial metrics
  downsideRisk: number;
  maxDrawdown: number;
  sharpeRatio: number;
  valueAtRisk95: number;
  annualizedVolatility: number;

  priceHistory: PriceData[];
}

export interface ApiResponse<T> {
  succeeded: boolean;
  message?: string;
  data?: T;
  errors?: string[];
}
