import { ChartNoAxesCombined } from 'lucide-react';
import { useId } from 'react';
import type { PriceData } from '../types';
import { formatUsdPrice } from '../utils/formatUsdPrice';
import { formatUtcAxisDate, formatUtcTooltipDate } from '../utils/formatUtcDate';
import { usePriceChart } from '../hooks/usePriceChart';

interface PriceChartProps {
  data: PriceData[];
  timeRange: number;
  currency?: string;
}
export function PriceChart({ data, timeRange, currency = 'USD' }: PriceChartProps) {
  const id = useId();
  const { containerRef, dimensions, model, activePoint, isKeyboardInspection, interactionProps } =
    usePriceChart(data);
  return (
    <section className="chart-panel" aria-labelledby={`${id}-heading`}>
      <div className="mb-5 flex items-center gap-3">
        <span className="feature-icon tone-blue">
          <ChartNoAxesCombined size={21} aria-hidden="true" />
        </span>
        <div className="min-w-0 flex-1">
          <h2 id={`${id}-heading`} className="section-title">
            {timeRange}-Day Price History
          </h2>
          <p className="mt-1 text-xs text-muted">
            {model
              ? `${formatUtcTooltipDate(model.points[0].timestamp)} – ${formatUtcTooltipDate(model.points[model.points.length - 1].timestamp)}`
              : 'No observations for this period'}
          </p>
        </div>
        <span className="hidden rounded-md border border-line bg-raised px-2 py-1 text-xs font-semibold text-accent sm:block">
          {currency}
        </span>
      </div>
      <div ref={containerRef} className="chart-canvas relative min-w-0">
        {model ? (
          <>
            <svg
              viewBox={`0 0 ${model.bounds.width} ${model.bounds.height}`}
              className="h-full w-full rounded-md touch-pan-y"
              role="img"
              tabIndex={0}
              aria-label={`${timeRange}-day price chart. Use left and right arrow keys to inspect values.`}
              {...interactionProps}
            >
              <defs>
                <linearGradient id={`${id}-area`} x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="var(--accent)" stopOpacity="0.24" />
                  <stop offset="100%" stopColor="var(--accent)" stopOpacity="0" />
                </linearGradient>
              </defs>
              {model.yTicks.map(tick => (
                <g key={tick.y}>
                  <line
                    x1={model.bounds.left}
                    x2={model.bounds.width - model.bounds.right}
                    y1={tick.y}
                    y2={tick.y}
                    stroke="var(--line)"
                    strokeDasharray="3 5"
                  />
                  <text
                    x={model.bounds.left - 10}
                    y={tick.y + 4}
                    textAnchor="end"
                    fill="var(--muted)"
                    fontSize="12"
                  >
                    {formatUsdPrice(tick.value, 'axis')}
                  </text>
                </g>
              ))}
              {model.xTicks.map((point, index) => (
                <text
                  key={point.timestamp}
                  x={point.x}
                  y={model.bounds.height - 10}
                  textAnchor={
                    index === 0 ? 'start' : index === model.xTicks.length - 1 ? 'end' : 'middle'
                  }
                  fill="var(--muted)"
                  fontSize="12"
                >
                  {formatUtcAxisDate(point.timestamp)}
                </text>
              ))}
              <path className="price-chart-area" d={model.areaPath} fill={`url(#${id}-area)`} />
              <path
                className="price-chart-line"
                pathLength={1}
                style={{
                  strokeDasharray: 1,
                }}
                d={model.linePath}
                fill="none"
                stroke="var(--accent)"
                strokeWidth="3"
                strokeLinejoin="round"
                strokeLinecap="round"
              />
              {model.points.length === 1 && (
                <circle cx={model.points[0].x} cy={model.points[0].y} r="4" fill="var(--accent)" />
              )}
              {activePoint && (
                <g aria-hidden="true">
                  <line
                    x1={activePoint.x}
                    x2={activePoint.x}
                    y1={model.bounds.top}
                    y2={model.bounds.height - model.bounds.bottom}
                    stroke="var(--control)"
                    strokeDasharray="4 4"
                  />
                  <circle
                    cx={activePoint.x}
                    cy={activePoint.y}
                    r="5"
                    fill="var(--accent)"
                    stroke="var(--panel)"
                    strokeWidth="2"
                  />
                </g>
              )}
            </svg>
            {activePoint && (
              <div
                aria-hidden="true"
                className="pointer-events-none absolute top-2 max-w-full rounded-lg border border-control bg-raised px-3 py-2 shadow-lg"
                style={{
                  left: Math.max(0, Math.min(dimensions.width - 208, activePoint.x - 104)),
                  width: Math.min(208, dimensions.width),
                }}
              >
                <p className="text-xs text-secondary">
                  {formatUtcTooltipDate(activePoint.timestamp)}
                </p>
                <p className="mt-1 break-words text-sm font-semibold tabular-nums">
                  {formatUsdPrice(activePoint.price)}
                </p>
              </div>
            )}
          </>
        ) : (
          <div
            className="grid h-full place-items-center rounded-lg border border-dashed border-line px-5 text-center text-sm text-secondary"
            role="status"
          >
            No price history is available for this period.
          </div>
        )}
      </div>
      <div className="mt-2 flex flex-wrap justify-between gap-1 text-xs text-muted">
        <p>Historical daily prices · UTC</p>
        <p>Touch or use ← → to inspect</p>
      </div>
      <span className="sr-only" aria-live="polite" aria-atomic="true">
        {activePoint && isKeyboardInspection
          ? `${formatUtcTooltipDate(activePoint.timestamp)}: ${formatUsdPrice(activePoint.price)}`
          : ''}
      </span>
    </section>
  );
}
