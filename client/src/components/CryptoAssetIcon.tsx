import { useState } from 'react';
import type { Asset } from '../types';

interface CryptoAssetIconProps {
  asset: Pick<Asset, 'icon' | 'name' | 'ticker'>;
  size?: 'small' | 'large';
}
export function CryptoAssetIcon({ asset, size = 'small' }: CryptoAssetIconProps) {
  const [loadedUrl, setLoadedUrl] = useState<string | null>(null);
  const [failedUrl, setFailedUrl] = useState<string | null>(null);
  const ready = loadedUrl === asset.icon && failedUrl !== asset.icon;
  return (
    <span
      className={`relative inline-flex shrink-0 items-center justify-center rounded-full bg-raised font-semibold text-secondary ${size === 'large' ? 'h-12 w-12 text-base' : 'h-6 w-6 text-[10px]'}`}
    >
      {!ready && (
        <span aria-label={`${asset.name} icon fallback`}>
          {asset.ticker.slice(0, 2).toUpperCase()}
        </span>
      )}
      {failedUrl !== asset.icon && (
        <img
          src={asset.icon}
          alt={ready ? `${asset.name} icon` : ''}
          className={`absolute inset-0 h-full w-full rounded-full object-contain ${ready ? 'opacity-100' : 'opacity-0'}`}
          onLoad={() => setLoadedUrl(asset.icon)}
          onError={() => setFailedUrl(asset.icon)}
        />
      )}
    </span>
  );
}
