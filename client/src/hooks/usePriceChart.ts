import { useEffect, useMemo, useRef, useState, type KeyboardEvent, type PointerEvent } from 'react';
import type { PriceData } from '../types';
import { createChartModel } from '../utils/priceChartModel';

interface ChartInspection {
  signature: string | undefined;
  index: number;
  keyboard: boolean;
}

export function usePriceChart(data: PriceData[]) {
  const containerRef = useRef<HTMLDivElement>(null);
  const [dimensions, setDimensions] = useState({ width: 1000, height: 310 });
  const model = useMemo(
    () => createChartModel(data, dimensions.width, dimensions.height),
    [data, dimensions]
  );
  // Identical series keep inspection state across background refreshes.
  const signature = useMemo(
    () => JSON.stringify(model?.points.map(({ timestamp, price }) => [timestamp, price])),
    [model]
  );
  const [inspection, setInspection] = useState<ChartInspection | null>(null);
  const activeIndex = inspection?.signature === signature ? inspection?.index : undefined;
  const activePoint = activeIndex === undefined ? null : model?.points[activeIndex];

  useEffect(() => {
    const container = containerRef.current;
    if (!container || typeof ResizeObserver === 'undefined') return;
    const observer = new ResizeObserver(([entry]) => {
      const width = Math.max(Math.round(entry.contentRect.width), 200);
      const height = Math.max(Math.round(entry.contentRect.height), 200);
      setDimensions(current =>
        current.width === width && current.height === height ? current : { width, height }
      );
    });
    observer.observe(container);
    return () => observer.disconnect();
  }, []);

  const inspect = (index: number, keyboard = false) =>
    setInspection({ signature, index, keyboard });
  const handlePointer = (event: PointerEvent<SVGSVGElement>) => {
    if (!model) return;
    const bounds = event.currentTarget.getBoundingClientRect();
    const x = ((event.clientX - bounds.left) / bounds.width) * model.bounds.width;
    inspect(
      model.points.reduce(
        (nearest, point, index) =>
          Math.abs(point.x - x) < Math.abs(model.points[nearest].x - x) ? index : nearest,
        0
      )
    );
  };
  const handleKeyDown = (event: KeyboardEvent<SVGSVGElement>) => {
    if (event.key === 'Escape') {
      setInspection(null);
      return;
    }
    if (!model || (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight')) return;
    event.preventDefault();
    inspect(
      Math.min(
        model.points.length - 1,
        Math.max(
          0,
          (activeIndex ?? model.points.length - 1) + (event.key === 'ArrowRight' ? 1 : -1)
        )
      ),
      true
    );
  };

  return {
    containerRef,
    dimensions,
    model,
    activePoint,
    isKeyboardInspection: inspection?.keyboard,
    interactionProps: {
      onPointerDown: handlePointer,
      onPointerMove: handlePointer,
      onPointerLeave: (event: PointerEvent<SVGSVGElement>) => {
        if (event.pointerType !== 'touch' && !inspection?.keyboard) setInspection(null);
      },
      onFocus: () => {
        if (model) inspect(activeIndex ?? model.points.length - 1, true);
      },
      onBlur: () => setInspection(null),
      onKeyDown: handleKeyDown,
    },
  };
}
