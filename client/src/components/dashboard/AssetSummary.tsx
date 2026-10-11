import { TrendingDown, TrendingUp } from 'lucide-react';
import type { Asset, PriceData, RiskAnalysisResponse } from '../../types';
import { formatUsdPrice } from '../../utils/formatUsdPrice';
import { formatUtcTime } from '../../utils/formatUtcDate';
import { CryptoAssetIcon } from '../CryptoAssetIcon';
import { usePriceFlash } from '../../hooks/usePriceFlash';

interface AssetSummaryProps {
  asset: Asset;
  price?: number;
  priceHistory?: PriceData[];
  loading: boolean;
  updatedAt?: number;
  quote?: RiskAnalysisResponse['currentQuote'];
}

export function AssetSummary({
  asset,
  price,
  priceHistory,
  loading,
  updatedAt,
  quote,
}: AssetSummaryProps) {
  // Calculate period price change
  const startPrice = priceHistory && priceHistory.length > 0 ? priceHistory[0].price : undefined;
  const priceChange =
    price !== undefined && startPrice !== undefined ? price - startPrice : undefined;
  const priceChangePercent =
    priceChange !== undefined && startPrice !== undefined && startPrice > 0
      ? (priceChange / startPrice) * 100
      : undefined;
  const isPositive = priceChangePercent !== undefined ? priceChangePercent >= 0 : null;
  const flash = usePriceFlash({
    assetId: asset.id,
    price,
    updatedAt,
    periodIsPositive: isPositive,
  });
  const flashClass = flash === 'up' ? 'price-flash-up' : flash === 'down' ? 'price-flash-down' : '';
  const formattedPrice = formatUsdPrice(price);

  return (
    <section
      aria-label="Selected asset and current price"
      className="asset-summary my-5 flex min-w-0 flex-col justify-between gap-4 sm:flex-row sm:items-center"
    >
      <div className="flex min-w-0 items-center gap-4">
        <span className="asset-icon-ring">
          <CryptoAssetIcon asset={asset} size="large" />
        </span>
        <div className="min-w-0">
          <p className="text-xs font-semibold uppercase tracking-wider text-accent">
            {asset.ticker} / {quote?.currency ?? 'USD'}
          </p>
          <h2 className="break-words text-2xl font-semibold tracking-tight sm:text-[28px]">
            {asset.name}
          </h2>
        </div>
      </div>
      <div className="min-w-0 sm:text-right">
        <div className="mb-1 flex items-center gap-1.5 sm:justify-end">
          <span
            className="inline-block h-2 w-2 rounded-full bg-positive motion-safe:animate-pulse"
            aria-hidden="true"
          />
          <p className="text-xs text-secondary">
            Current price <span className="text-muted">· {quote?.currency ?? 'USD'}</span>
          </p>
        </div>
        {quote && (
          <p className="mb-1 text-xs text-muted">
            {quote.source} · Fetched at{' '}
            <time dateTime={quote.fetchedAt}>{formatUtcTime(quote.fetchedAt)}</time>
            {quote.sourceUpdatedAt && (
              <> · Source updated at {formatUtcTime(quote.sourceUpdatedAt)}</>
            )}
          </p>
        )}
        {loading ? (
          <div
            aria-label="Loading current price"
            className="skeleton h-10 w-56 max-w-full sm:h-12"
          />
        ) : (
          <div>
            <p
              data-flash={flash ?? undefined}
              className={`break-words font-semibold tracking-tight tabular-nums transition-colors duration-200 ${
                formattedPrice === 'Unavailable'
                  ? 'text-xl text-muted'
                  : 'text-[32px] leading-tight sm:text-[40px]'
              } ${flashClass}`}
            >
              {formattedPrice}
            </p>
            {priceChangePercent !== undefined && (
              <div className="mt-1 flex flex-wrap items-center gap-2 sm:justify-end">
                <span
                  data-testid="price-change-badge"
                  className={`inline-flex items-center gap-1 rounded-md px-2 py-0.5 text-xs font-semibold tabular-nums ${
                    isPositive
                      ? 'border border-emerald-500/30 bg-emerald-500/15 text-emerald-400'
                      : 'border border-rose-500/30 bg-rose-500/15 text-rose-400'
                  }`}
                >
                  {isPositive ? (
                    <TrendingUp size={13} aria-hidden="true" />
                  ) : (
                    <TrendingDown size={13} aria-hidden="true" />
                  )}
                  {isPositive ? '+' : ''}
                  {priceChangePercent.toFixed(2)}%
                  <span className="text-[10px] opacity-75">
                    ({priceHistory ? `${priceHistory.length}D` : ''})
                  </span>
                </span>
                {priceChange !== undefined && (
                  <span className="text-xs text-muted tabular-nums">
                    {priceChange < 0 ? '-' : priceChange > 0 ? '+' : ''}
                    {formatUsdPrice(Math.abs(priceChange))}
                  </span>
                )}
              </div>
            )}
          </div>
        )}
      </div>
    </section>
  );
}
