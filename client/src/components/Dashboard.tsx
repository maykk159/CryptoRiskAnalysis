import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getRiskAnalysis, getErrorMessage } from '../services/api';
import { ASSETS } from '../constants/assets';
import { TimeRangeSelector } from './dashboard/TimeRangeSelector';
import { AssetSelector } from './AssetSelector';
import { RiskScoreCard } from './dashboard/RiskScoreCard';
import { AdvancedMetrics } from './dashboard/AdvancedMetrics';
import { PriceChart } from './PriceChart';

export function Dashboard() {
  const [selectedAssetId, setSelectedAssetId] = useState<string>('bitcoin');
  const [selectedTimeRange, setSelectedTimeRange] = useState<number>(30);

  const selectedAsset = ASSETS.find(a => a.id === selectedAssetId) ?? ASSETS[0];

  const { data, isLoading, error, isRefetchError } = useQuery({
    queryKey: ['risk', selectedAssetId, selectedTimeRange],
    queryFn: ({ signal }) => getRiskAnalysis(selectedAssetId, selectedTimeRange, signal),
  });

  const errorMessage = !data && error ? getErrorMessage(error, selectedAsset.name) : null;
  const refetchErrorMessage =
    data && isRefetchError && error ? getErrorMessage(error, selectedAsset.name) : null;

  const latestPrice =
    data?.priceHistory && data.priceHistory.length > 0
      ? data.priceHistory[data.priceHistory.length - 1].price
      : undefined;

  return (
    <div className="min-h-screen bg-gray-900 text-white p-4 sm:p-8">
      <div className="max-w-7xl mx-auto">
        <header className="mb-8 sm:mb-10">
          <h1 className="text-3xl sm:text-4xl font-extrabold tracking-tight flex items-center gap-3 flex-wrap">
            <span className="bg-clip-text text-transparent bg-gradient-to-r from-blue-400 to-indigo-500">
              CIPHER
            </span>
            <span className="hidden sm:inline text-gray-700 font-light text-2xl">|</span>
            <span className="text-gray-400 font-light text-2xl">Crypto Risk Intelligence</span>
          </h1>
          <p className="text-gray-400 mt-2 text-sm sm:text-base">
            Advanced quantitative financial risk assessment tool for crypto assets
          </p>
        </header>

        {/* Asset Selector */}
        <AssetSelector selectedAsset={selectedAssetId} onSelectAsset={setSelectedAssetId} />

        {/* Time Range Selector */}
        <TimeRangeSelector value={selectedTimeRange} onChange={setSelectedTimeRange} />

        {/* Loading */}
        {isLoading && (
          <div
            className="flex justify-center items-center h-64"
            role="status"
            aria-live="polite"
            aria-atomic="true"
          >
            <div
              className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-500"
              aria-hidden="true"
            />
            <span className="sr-only">Loading risk analysis for {selectedAsset.name}.</span>
          </div>
        )}

        {/* Error */}
        {errorMessage && (
          <div
            className="bg-red-900/50 border border-red-500 text-red-200 p-4 rounded-lg mb-6"
            role="alert"
            aria-live="assertive"
            aria-atomic="true"
          >
            {errorMessage}
          </div>
        )}

        {/* A background refresh failure must not hide the last successful result. */}
        {refetchErrorMessage && (
          <div
            className="bg-amber-900/40 border border-amber-500 text-amber-100 p-4 rounded-lg mb-6"
            role="status"
            aria-live="polite"
            aria-atomic="true"
          >
            Refresh failed: {refetchErrorMessage} Showing the last successfully loaded data.
          </div>
        )}

        {/* Content */}
        {!isLoading && data && (
          <div className="grid grid-cols-1 gap-6 sm:gap-8 min-w-0">
            <RiskScoreCard data={data} asset={selectedAsset} currentPrice={latestPrice} />
            <AdvancedMetrics data={data} />
            <PriceChart data={data.priceHistory} timeRange={selectedTimeRange} />
          </div>
        )}
      </div>
    </div>
  );
}
