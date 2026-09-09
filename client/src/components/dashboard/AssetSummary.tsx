import { useEffect, useRef, useState } from 'react';
import { TrendingDown, TrendingUp } from 'lucide-react';
import type { Asset, PriceData } from '../../types';
import { formatUsdPrice } from '../../utils/formatUsdPrice';
import { CryptoAssetIcon } from '../CryptoAssetIcon';

export function AssetSummary({
  asset,
  price,
  priceHistory,
  loading,
  updatedAt,
}: {
  asset: Asset;
  price?: number;
  priceHistory?: PriceData[];
  loading: boolean;
  updatedAt?: number;
}) {
  const [flash, setFlash] = useState<'up' | 'down' | null>(null);
  const prevPriceRef = useRef<number | undefined>(price);
  const prevAssetIdRef = useRef<string>(asset.id);
  const prevUpdatedAtRef = useRef<number | undefined>(updatedAt);

  // Calculate period price change
  const startPrice = priceHistory && priceHistory.length > 0 ? priceHistory[0].price : undefined;
  const priceChange =
    price !== undefined && startPrice !== undefined ? price - startPrice : undefined;
  const priceChangePercent =
    priceChange !== undefined && startPrice !== undefined && startPrice > 0
      ? (priceChange / startPrice) * 100
      : undefined;
  const isPositive = priceChangePercent !== undefined ? priceChangePercent >= 0 : null;

  useEffect(() => {
    // If the asset changed, reset refs without flashing
    if (prevAssetIdRef.current !== asset.id) {
      prevAssetIdRef.current = asset.id;
      prevPriceRef.current = price;
      prevUpdatedAtRef.current = updatedAt;
      setFlash(null);
      return;
    }

    // Condition 1: Actual price change detected
    if (
      price !== undefined &&
      prevPriceRef.current !== undefined &&
      price !== prevPriceRef.current
    ) {
      const direction = price > prevPriceRef.current ? 'up' : 'down';
      setFlash(direction);
      prevPriceRef.current = price;
      prevUpdatedAtRef.current = updatedAt;

      const timer = setTimeout(() => {
        setFlash(null);
      }, 1400);

      return () => clearTimeout(timer);
    }

    // Condition 2: Data refresh occurred (updatedAt changed) for the current asset
    if (
      updatedAt !== undefined &&
      prevUpdatedAtRef.current !== undefined &&
      updatedAt !== prevUpdatedAtRef.current &&
      price !== undefined
    ) {
      const direction =
        prevPriceRef.current !== undefined && price !== prevPriceRef.current
          ? price > prevPriceRef.current
            ? 'up'
            : 'down'
          : isPositive !== null
            ? isPositive
              ? 'up'
              : 'down'
            : 'up';

      setFlash(direction);
      prevPriceRef.current = price;
      prevUpdatedAtRef.current = updatedAt;

      const timer = setTimeout(() => {
        setFlash(null);
      }, 1400);

      return () => clearTimeout(timer);
    }

    if (price !== undefined && prevPriceRef.current === undefined) {
      prevPriceRef.current = price;
    }
    if (updatedAt !== undefined && prevUpdatedAtRef.current === undefined) {
      prevUpdatedAtRef.current = updatedAt;
    }
  }, [asset.id, price, updatedAt, isPositive]);

  const flashClass = flash === 'up' ? 'price-flash-up' : flash === 'down' ? 'price-flash-down' : '';

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
            {asset.ticker} / USD
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
            Current price <span className="text-muted">· USD</span>
          </p>
        </div>
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
                formatUsdPrice(price) === 'Unavailable'
                  ? 'text-xl text-muted'
                  : 'text-[32px] leading-tight sm:text-[40px]'
              } ${flashClass}`}
            >
              {formatUsdPrice(price)}
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
