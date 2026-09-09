import { useQuery } from '@tanstack/react-query';
import { RefreshCw, TriangleAlert } from 'lucide-react';
import { getRiskAnalysis, getErrorMessage } from '../services/api';
import { ASSETS } from '../constants/assets';
import { useAnalysisUrlState } from '../hooks/useAnalysisUrlState';
import { TimeRangeSelector } from './dashboard/TimeRangeSelector';
import { AssetSelector } from './AssetSelector';
import { RiskScoreCard } from './dashboard/RiskScoreCard';
import { AdvancedMetrics } from './dashboard/AdvancedMetrics';
import { DashboardSkeleton } from './dashboard/DashboardSkeleton';
import { DataStatus } from './dashboard/DataStatus';
import { AssetSummary } from './dashboard/AssetSummary';
import { PriceChart } from './PriceChart';

export function Dashboard() {
  const {
    assetId: selectedAssetId,
    days: selectedTimeRange,
    setAssetId: setSelectedAssetId,
    setDays: setSelectedTimeRange,
  } = useAnalysisUrlState();

  const selectedAsset = ASSETS.find(a => a.id === selectedAssetId) ?? {
    id: selectedAssetId,
    name: selectedAssetId.charAt(0).toUpperCase() + selectedAssetId.slice(1),
    ticker: selectedAssetId.toUpperCase(),
    icon: '',
  };
  const {
    data,
    isPending,
    isFetching,
    fetchStatus,
    error,
    isRefetchError,
    refetch,
    dataUpdatedAt,
  } = useQuery({
    queryKey: ['risk', selectedAssetId, selectedTimeRange],
    queryFn: ({ signal }) => getRiskAnalysis(selectedAssetId, selectedTimeRange, signal),
    refetchInterval: 60_000,
  });
  const paused = fetchStatus === 'paused';
  const retryAnalysis = () => {
    void refetch({ cancelRefetch: false });
  };
  return (
    <div className="min-h-screen bg-canvas px-4 py-5 text-ink sm:px-6 lg:px-8">
      <div className="mx-auto max-w-[1280px]">
        <header className="flex flex-wrap items-center justify-between gap-2 border-b border-line pb-5">
          <div className="flex items-center gap-3">
            <img
              src={`${import.meta.env.BASE_URL}favicon.svg`}
              alt=""
              width={32}
              height={32}
              className="brand-mark h-8 w-8 shrink-0 sm:h-10 sm:w-10"
            />
            <div>
              <h1 className="text-xl font-bold tracking-[0.16em] sm:text-2xl">CIPHER</h1>
              <p className="text-[11px] text-secondary sm:text-xs">Crypto Risk Intelligence</p>
            </div>
          </div>
          <DataStatus
            hasData={!!data}
            isFetching={isFetching}
            isPaused={paused}
            hasError={!!error}
            updatedAt={dataUpdatedAt}
            assetName={selectedAsset.name}
            days={selectedTimeRange}
          />
        </header>
        <main>
          <section
            aria-label="Analysis controls"
            className="analysis-toolbar mt-5 flex flex-wrap items-end gap-3 sm:gap-4"
          >
            <div className="w-full min-w-0 sm:min-w-60 sm:flex-1">
              <AssetSelector selectedAsset={selectedAssetId} onSelectAsset={setSelectedAssetId} />
            </div>
            <TimeRangeSelector value={selectedTimeRange} onChange={setSelectedTimeRange} />
            <button
              type="button"
              onClick={retryAnalysis}
              disabled={isFetching || paused}
              className="control-button mb-1"
            >
              <RefreshCw
                size={16}
                aria-hidden="true"
                className={isFetching ? 'motion-safe:animate-spin' : ''}
              />
              Refresh
            </button>
          </section>
          <AssetSummary
            asset={selectedAsset}
            price={data?.currentPrice}
            priceHistory={data?.priceHistory}
            loading={isPending}
            updatedAt={dataUpdatedAt}
          />
          {(error || paused) && (
            <div
              className="mb-4 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-control bg-panel p-4"
              role={data ? undefined : 'alert'}
            >
              <div className="flex min-w-0 flex-1 items-start gap-3">
                <TriangleAlert
                  size={18}
                  className="mt-0.5 shrink-0 text-warning"
                  aria-hidden="true"
                />
                <p className="text-sm text-secondary">
                  {paused ? (
                    'Connection paused. Requests will resume when you are back online.'
                  ) : (
                    <>
                      {data && 'Refresh failed: '}
                      {getErrorMessage(error, selectedAsset.name)}
                    </>
                  )}
                  {data && ' Showing the last successfully loaded data.'}
                </p>
              </div>
              {!paused && (
                <button
                  type="button"
                  className="control-button"
                  onClick={retryAnalysis}
                  disabled={isFetching}
                >
                  {isFetching ? 'Retrying…' : 'Try again'}
                </button>
              )}
            </div>
          )}
          <div aria-busy={isFetching}>
            {isPending && <DashboardSkeleton />}
            {data && (
              <>
                <div className="analysis-grid">
                  <RiskScoreCard key={`${selectedAssetId}-${selectedTimeRange}`} data={data} />
                  <PriceChart
                    key={`${selectedAssetId}-${selectedTimeRange}`}
                    data={data.priceHistory}
                    timeRange={selectedTimeRange}
                  />
                </div>
                <AdvancedMetrics data={data} />
              </>
            )}
            {!data && !isPending && (
              <section className="panel py-12 text-center">
                <h2 className="section-title">Analysis unavailable</h2>
                <p className="mt-2 text-sm text-secondary">
                  Try again or choose another asset or period above.
                </p>
              </section>
            )}
          </div>
          <footer className="mt-6 border-t border-line py-4 text-xs leading-relaxed text-muted">
            <p>
              Risk scores use a 0–100 scale: low below 30, medium from 30, high from 70. They are
              analytical indicators, not probabilities or investment advice.
            </p>
            <p className="mt-1">
              Automatic fetch every 60 seconds while active. Results may be cached by the server.
              “Last fetched” records browser receipt, not the source price observation time.
            </p>
            {isRefetchError && (
              <p className="mt-1 text-warning">
                The displayed analysis is from the last successful fetch for this selection.
              </p>
            )}
          </footer>
        </main>
      </div>
    </div>
  );
}
