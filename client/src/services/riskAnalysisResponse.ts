import type { RiskAnalysisResponse } from '../types';

export function isJsonObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function isFiniteNumber(value: unknown): value is number {
  return typeof value === 'number' && Number.isFinite(value);
}

function isPrice(value: unknown) {
  return isFiniteNumber(value) && value > 0;
}

function isTimestamp(value: unknown) {
  return isFiniteNumber(value) && Number.isSafeInteger(value) && Math.abs(value) <= 8.64e15;
}

function isNonEmptyString(value: unknown): value is string {
  return typeof value === 'string' && value.trim().length > 0;
}

function isDateString(value: unknown) {
  return typeof value === 'string' && Number.isFinite(Date.parse(value));
}

function isQuote(value: unknown) {
  return (
    isJsonObject(value) &&
    isPrice(value.price) &&
    isNonEmptyString(value.source) &&
    isNonEmptyString(value.currency) &&
    isDateString(value.fetchedAt) &&
    (value.sourceUpdatedAt === null || isDateString(value.sourceUpdatedAt))
  );
}

function isMethodology(value: unknown) {
  return (
    isJsonObject(value) &&
    isNonEmptyString(value.version) &&
    isNonEmptyString(value.riskLevel) &&
    [value.observationCount, value.returnCount].every(
      count => isFiniteNumber(count) && Number.isSafeInteger(count) && count >= 0
    ) &&
    isTimestamp(value.periodStart) &&
    isTimestamp(value.periodEnd) &&
    [
      value.volatilityWeight,
      value.trendWeight,
      value.volumeWeight,
      value.volatilityContribution,
      value.trendContribution,
      value.volumeContribution,
    ].every(isFiniteNumber) &&
    Array.isArray(value.warnings) &&
    value.warnings.every(warning => typeof warning === 'string')
  );
}

export function isRiskAnalysisResponse(value: unknown): value is RiskAnalysisResponse {
  return (
    isJsonObject(value) &&
    isNonEmptyString(value.assetId) &&
    isPrice(value.currentPrice) &&
    [value.compositeRiskScore, value.volatilityScore, value.trendScore, value.volumeScore].every(
      score => isFiniteNumber(score) && score >= 0 && score <= 100
    ) &&
    [value.downsideRisk, value.maxDrawdown, value.valueAtRisk95, value.annualizedVolatility].every(
      isFiniteNumber
    ) &&
    (value.sharpeRatio === null || isFiniteNumber(value.sharpeRatio)) &&
    Array.isArray(value.priceHistory) &&
    value.priceHistory.length > 0 &&
    value.priceHistory.every(
      point => isJsonObject(point) && isTimestamp(point.timestamp) && isPrice(point.price)
    ) &&
    (value.currentQuote == null || isQuote(value.currentQuote)) &&
    (value.methodology == null || isMethodology(value.methodology))
  );
}
