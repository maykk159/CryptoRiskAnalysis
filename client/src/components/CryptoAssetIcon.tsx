import { useState } from 'react';
import type { Asset } from '../types';

interface CryptoAssetIconProps {
  asset: Pick<Asset, 'icon' | 'name' | 'ticker'>;
  size?: 'small' | 'large';
}

const sizeClasses = {
  small: {
    image: 'w-6 h-6 object-contain shrink-0',
    fallback: 'w-6 h-6 text-xs',
  },
  large: {
    image: 'w-12 h-12 rounded-full object-contain bg-white p-1 shrink-0',
    fallback: 'w-12 h-12 text-lg border-2 border-gray-600',
  },
} as const;

export function CryptoAssetIcon({ asset, size = 'small' }: CryptoAssetIconProps) {
  const [failedUrl, setFailedUrl] = useState<string | null>(null);
  const classes = sizeClasses[size];

  if (failedUrl === asset.icon) {
    return (
      <span
        className={`${classes.fallback} rounded-full bg-gray-700 flex items-center justify-center font-bold text-gray-300 shrink-0`}
        aria-label={`${asset.name} icon fallback`}
      >
        {asset.ticker.slice(0, 2).toUpperCase()}
      </span>
    );
  }

  return (
    <img
      src={asset.icon}
      alt={`${asset.name} icon`}
      className={classes.image}
      onError={() => setFailedUrl(asset.icon)}
    />
  );
}
