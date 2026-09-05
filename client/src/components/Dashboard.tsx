import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { RefreshCw } from 'lucide-react';
import { getRiskAnalysis, getErrorMessage } from '../services/api';
import { ASSETS } from '../constants/assets';
import { TimeRangeSelector } from './dashboard/TimeRangeSelector';
import { AssetSelector } from './AssetSelector';
import { RiskScoreCard } from './dashboard/RiskScoreCard';
import { AdvancedMetrics } from './dashboard/AdvancedMetrics';
import { DashboardSkeleton } from './dashboard/DashboardSkeleton';
import { PriceChart } from './PriceChart';

export function Dashboard() {
  const [selectedAssetId, setSelectedAssetId] = useState<string>('bitcoin');
  const [selectedTimeRange, setSelectedTimeRange] = useState<number>(30);

  const selectedAsset = ASSETS.find(a => a.id === selectedAssetId) ?? ASSETS[0];

  const { data, isPending, isFetching, error, isRefetchError, refetch } = useQuery({
    queryKey: ['risk', selectedAssetId, selectedTimeRange],
    queryFn: ({ signal }) => getRiskAnalysis(selectedAssetId, selectedTimeRange, signal),
  });

  const errorMessage = !data && error ? getErrorMessage(error, selectedAsset.name) : null;
  const refetchErrorMessage =
    data && isRefetchError && error ? getErrorMessage(error, selectedAsset.name) : null;
  const visibleErrorMessage = errorMessage ?? refetchErrorMessage;
  const retryAnalysis = () => {
    void refetch({ cancelRefetch: false });
  };

  const latestPrice =
    data?.priceHistory && data.priceHistory.length > 0
      ? data.priceHistory[data.priceHistory.length - 1].price
      : undefined;

  return (
    <div className="min-h-screen bg-gray-900 text-white p-4 sm:p-8">
      <div className="max-w-7xl mx-auto">
        <header className="mb-8 sm:mb-10">
          <h1 className="text-3xl sm:text-4xl font-extrabold tracking-tight flex items-center gap-x-3 gap-y-2 flex-wrap">
            <span className="inline-flex items-center gap-3 shrink-0">
              <img
                src={`${import.meta.env.BASE_URL}favicon.svg`}
                alt=""
                width={48}
                height={48}
                className="w-10 h-10 sm:w-12 sm:h-12 shrink-0"
              />
              <span className="bg-clip-text text-transparent bg-gradient-to-r from-blue-400 to-indigo-500">
                CIPHER
              </span>
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

        <TimeRangeSelector value={selectedTimeRange} onChange={setSelectedTimeRange} />

        <p className="sr-only" role="status" aria-atomic="true">
          {isFetching
            ? `${data ? 'Refreshing' : 'Loading'} ${selectedTimeRange}-day risk analysis for ${selectedAsset.name}.`
            : data && !isRefetchError
              ? `${selectedTimeRange}-day risk analysis for ${selectedAsset.name} is ready.`
              : ''}
        </p>

        {/* A background refresh failure must not hide the last successful result. */}
        {visibleErrorMessage && (
          <div
            className={`flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 border p-4 rounded-lg mb-6 ${
              data
                ? 'bg-amber-900/40 border-amber-500 text-amber-100'
                : 'bg-red-900/50 border-red-500 text-red-200'
            }`}
            role={data ? 'status' : 'alert'}
            aria-atomic="true"
          >
            <p>
              {data && 'Refresh failed: '}
              {visibleErrorMessage}
              {data && ' Showing the last successfully loaded data.'}
            </p>
            <div className="shrink-0">
              <button
                type="button"
                onClick={retryAnalysis}
                disabled={isFetching}
                className="inline-flex min-h-10 items-center justify-center gap-2 rounded-lg border border-white/20 px-3 py-2 text-sm font-medium transition-colors hover:bg-white/10 disabled:cursor-not-allowed disabled:opacity-60 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-300 focus-visible:ring-offset-2 focus-visible:ring-offset-gray-900"
              >
                <RefreshCw
                  size={16}
                  aria-hidden="true"
                  className={isFetching ? 'motion-safe:animate-spin' : undefined}
                />
                {isFetching ? 'Retrying…' : 'Try again'}
              </button>
            </div>
          </div>
        )}

        <div aria-busy={isFetching}>
          {isPending && <DashboardSkeleton />}
          {data && (
            <div className="grid grid-cols-1 gap-6 sm:gap-8 min-w-0">
              <RiskScoreCard data={data} asset={selectedAsset} currentPrice={latestPrice} />
              <AdvancedMetrics data={data} />
              <PriceChart data={data.priceHistory} timeRange={selectedTimeRange} />
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
