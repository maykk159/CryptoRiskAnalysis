import type { Asset } from '../../types';
import { formatUsdPrice } from '../../utils/formatUsdPrice';
import { CryptoAssetIcon } from '../CryptoAssetIcon';

export function AssetSummary({
  asset,
  price,
  loading,
}: {
  asset: Asset;
  price?: number;
  loading: boolean;
}) {
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
        <p className="mb-1 text-xs text-secondary">
          Current price <span className="text-muted">· USD</span>
        </p>
        {loading ? (
          <div
            aria-label="Loading current price"
            className="skeleton h-10 w-56 max-w-full sm:h-12"
          />
        ) : (
          <p
            className={`break-words font-semibold tracking-tight tabular-nums ${formatUsdPrice(price) === 'Unavailable' ? 'text-xl text-muted' : 'text-[32px] leading-tight sm:text-[40px]'}`}
          >
            {formatUsdPrice(price)}
          </p>
        )}
      </div>
    </section>
  );
}
