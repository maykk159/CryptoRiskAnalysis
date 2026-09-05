import { useEffect, useMemo, useRef, useState, type KeyboardEvent, type PointerEvent } from 'react';
import { TrendingUp } from 'lucide-react';
import type { PriceData } from '../types';
import { formatUsdPrice } from '../utils/formatUsdPrice';
import { formatUtcAxisDate, formatUtcTooltipDate } from '../utils/formatUtcDate';
import { createChartModel } from '../utils/priceChartModel';

interface PriceChartProps {
  data: PriceData[];
  timeRange: number;
}

export function PriceChart({ data, timeRange }: PriceChartProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const [dimensions, setDimensions] = useState({ width: 1_000, height: 360 });
  const model = useMemo(
    () => createChartModel(data, dimensions.width, dimensions.height),
    [data, dimensions]
  );
  const [activeIndex, setActiveIndex] = useState<number | null>(null);
  const activePoint = activeIndex === null ? null : model?.points[activeIndex];

  useEffect(() => {
    const container = containerRef.current;
    if (!container || typeof ResizeObserver === 'undefined') return;

    const observer = new ResizeObserver(([entry]) => {
      const width = Math.max(Math.round(entry.contentRect.width), 240);
      const height = Math.max(Math.round(entry.contentRect.height), 200);
      setDimensions(current =>
        current.width === width && current.height === height ? current : { width, height }
      );
    });
    observer.observe(container);
    return () => observer.disconnect();
  }, []);

  const handlePointerMove = (event: PointerEvent<SVGSVGElement>) => {
    if (!model) return;
    const bounds = event.currentTarget.getBoundingClientRect();
    const pointerX = ((event.clientX - bounds.left) / bounds.width) * model.bounds.width;
    const nearestIndex = model.points.reduce(
      (nearest, point, index) =>
        Math.abs(point.x - pointerX) < Math.abs(model.points[nearest].x - pointerX)
          ? index
          : nearest,
      0
    );
    setActiveIndex(nearestIndex);
  };

  const handleKeyDown = (event: KeyboardEvent<SVGSVGElement>) => {
    if (!model || (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight')) return;
    event.preventDefault();
    const direction = event.key === 'ArrowRight' ? 1 : -1;
    setActiveIndex(current =>
      Math.min(
        model.points.length - 1,
        Math.max(0, (current ?? model.points.length - 1) + direction)
      )
    );
  };

  return (
    <section className="bg-gray-800 rounded-2xl p-4 sm:p-7 shadow-lg border border-gray-700 h-[500px] flex flex-col">
      <div className="flex items-center gap-4 mb-6">
        <div className="p-3 bg-indigo-500/10 border border-indigo-500/30 rounded-xl shrink-0 shadow-[0_0_15px_rgba(99,102,241,0.2)]">
          <TrendingUp className="w-6 h-6 text-indigo-400" aria-hidden="true" />
        </div>
        <div>
          <h2 className="text-xl font-bold text-white">{timeRange}-Day Price History</h2>
          <p className="text-gray-400 text-sm mt-0.5">
            Historical price performance over the last {timeRange} days
          </p>
        </div>
      </div>

      <div ref={containerRef} className="flex-1 min-h-0">
        {model ? (
          <svg
            viewBox={`0 0 ${model.bounds.width} ${model.bounds.height}`}
            className="h-full w-full overflow-visible outline-none"
            role="img"
            aria-label={`${timeRange}-day price chart. Use left and right arrow keys to inspect values.`}
            tabIndex={0}
            onPointerMove={handlePointerMove}
            onPointerLeave={() => setActiveIndex(null)}
            onFocus={() => setActiveIndex(current => current ?? model.points.length - 1)}
            onBlur={() => setActiveIndex(null)}
            onKeyDown={handleKeyDown}
          >
            <defs>
              <linearGradient id="price-area-gradient" x1="0" y1="0" x2="0" y2="1">
                <stop offset="5%" stopColor="#6366f1" stopOpacity="0.4" />
                <stop offset="95%" stopColor="#6366f1" stopOpacity="0" />
              </linearGradient>
            </defs>

            {model.yTicks.map(tick => (
              <g key={tick.y}>
                <line
                  x1={model.bounds.left}
                  x2={model.bounds.width - model.bounds.right}
                  y1={tick.y}
                  y2={tick.y}
                  stroke="#374151"
                  strokeDasharray="5 5"
                />
                <text
                  x={model.bounds.left - 12}
                  y={tick.y + 4}
                  textAnchor="end"
                  fill="#9ca3af"
                  fontSize="12"
                >
                  {formatUsdPrice(tick.value)}
                </text>
              </g>
            ))}

            {model.xTicks.map(point => (
              <text
                key={point.timestamp}
                x={point.x}
                y={model.bounds.height - 12}
                textAnchor="middle"
                fill="#9ca3af"
                fontSize="12"
              >
                {formatUtcAxisDate(point.timestamp)}
              </text>
            ))}

            <path
              className="price-chart-area"
              d={model.areaPath}
              fill="url(#price-area-gradient)"
            />
            <path
              className="price-chart-line"
              d={model.linePath}
              pathLength={1}
              fill="none"
              stroke="#6366f1"
              strokeWidth="3"
              strokeLinejoin="round"
              strokeLinecap="round"
            />

            {activePoint && (
              <g aria-hidden="true">
                <line
                  x1={activePoint.x}
                  x2={activePoint.x}
                  y1={model.bounds.top}
                  y2={model.bounds.height - model.bounds.bottom}
                  stroke="#818cf8"
                  strokeDasharray="4 4"
                />
                <circle cx={activePoint.x} cy={activePoint.y} r="7" fill="#c7d2fe" />
                <circle cx={activePoint.x} cy={activePoint.y} r="4" fill="#6366f1" />
                <g
                  transform={`translate(${Math.min(
                    model.bounds.width - 184,
                    Math.max(model.bounds.left, activePoint.x - 80)
                  )}, ${Math.max(model.bounds.top, activePoint.y - 72)})`}
                >
                  <rect
                    width="168"
                    height="56"
                    rx="10"
                    fill="#111827"
                    fillOpacity="0.94"
                    stroke="#4f46e5"
                  />
                  <text x="12" y="21" fill="#d1d5db" fontSize="12">
                    {formatUtcTooltipDate(activePoint.timestamp)}
                  </text>
                  <text x="12" y="43" fill="#818cf8" fontSize="14" fontWeight="700">
                    {formatUsdPrice(activePoint.price)}
                  </text>
                </g>
              </g>
            )}
          </svg>
        ) : (
          <div className="h-full grid place-items-center text-gray-400" role="status">
            No price history is available for this period.
          </div>
        )}
      </div>
      <span className="sr-only" aria-live="polite">
        {activePoint
          ? `${formatUtcTooltipDate(activePoint.timestamp)}: ${formatUsdPrice(activePoint.price)}`
          : ''}
      </span>
    </section>
  );
}
