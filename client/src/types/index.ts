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
  currentPrice: number;
  compositeRiskScore: number;
  volatilityScore: number;
  trendScore: number;
  volumeScore: number;

  // Advanced financial metrics
  downsideRisk: number;
  maxDrawdown: number;
  sharpeRatio: number | null;
  valueAtRisk95: number;
  annualizedVolatility: number;

  priceHistory: PriceData[];
  methodology?: {
    version: string;
    riskLevel: string;
    observationCount: number;
    returnCount: number;
    periodStart: number;
    periodEnd: number;
    volatilityWeight: number;
    trendWeight: number;
    volumeWeight: number;
    volatilityContribution: number;
    trendContribution: number;
    volumeContribution: number;
    warnings: string[];
  };
}

export interface ApiResponse<T> {
  succeeded: boolean;
  message?: string;
  data?: T;
  errors?: string[];
}
