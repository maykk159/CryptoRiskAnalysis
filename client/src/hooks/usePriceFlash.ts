import { useEffect, useEffectEvent, useRef, useState } from 'react';

type FlashDirection = 'up' | 'down';

interface PriceFlashOptions {
  assetId: string;
  price?: number;
  updatedAt?: number;
  periodIsPositive: boolean | null;
}

export function usePriceFlash({ assetId, price, updatedAt, periodIsPositive }: PriceFlashOptions) {
  const [flash, setFlash] = useState<FlashDirection | null>(null);
  const prevPriceRef = useRef(price);
  const prevAssetIdRef = useRef(assetId);
  const prevUpdatedAtRef = useRef(updatedAt);
  const periodDirection = useEffectEvent(() => (periodIsPositive === false ? 'down' : 'up'));

  useEffect(() => {
    if (prevAssetIdRef.current !== assetId) {
      prevAssetIdRef.current = assetId;
      prevPriceRef.current = price;
      prevUpdatedAtRef.current = updatedAt;
      setFlash(null);
      return;
    }

    let direction: FlashDirection | undefined;
    if (
      price !== undefined &&
      prevPriceRef.current !== undefined &&
      price !== prevPriceRef.current
    ) {
      direction = price > prevPriceRef.current ? 'up' : 'down';
    } else if (
      updatedAt !== undefined &&
      prevUpdatedAtRef.current !== undefined &&
      updatedAt !== prevUpdatedAtRef.current &&
      price !== undefined
    ) {
      direction = periodDirection();
    }

    if (direction !== undefined) {
      setFlash(direction);
      prevPriceRef.current = price;
      prevUpdatedAtRef.current = updatedAt;
      const timer = setTimeout(() => setFlash(null), 1400);
      return () => clearTimeout(timer);
    }

    if (price !== undefined && prevPriceRef.current === undefined) {
      prevPriceRef.current = price;
    }
    if (updatedAt !== undefined && prevUpdatedAtRef.current === undefined) {
      prevUpdatedAtRef.current = updatedAt;
    }
  }, [assetId, price, updatedAt]);

  return flash;
}
