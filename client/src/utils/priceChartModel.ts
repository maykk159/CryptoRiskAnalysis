import type { PriceData } from '../types';

export interface ChartBounds {
  width: number;
  height: number;
  left: number;
  right: number;
  top: number;
  bottom: number;
}

export interface ChartPoint extends PriceData {
  x: number;
  y: number;
}

export interface ChartModel {
  bounds: ChartBounds;
  points: ChartPoint[];
  linePath: string;
  areaPath: string;
  xTicks: ChartPoint[];
  yTicks: Array<{ value: number; y: number }>;
}

const getTickIndexes = (length: number, maximum: number) => {
  if (length <= maximum) return Array.from({ length }, (_, index) => index);

  return Array.from(
    new Set(
      Array.from({ length: maximum }, (_, index) =>
        Math.round((index * (length - 1)) / (maximum - 1))
      )
    )
  );
};

export const createChartModel = (
  data: PriceData[],
  width = 1_000,
  height = 360
): ChartModel | null => {
  const validData = data.filter(
    point => Number.isFinite(point.timestamp) && Number.isFinite(point.price)
  );
  if (validData.length === 0) return null;

  const prices = validData.map(point => point.price);
  const timestamps = validData.map(point => point.timestamp);
  const minimumPrice = Math.min(...prices);
  const maximumPrice = Math.max(...prices);
  const priceRange = maximumPrice - minimumPrice || Math.max(Math.abs(maximumPrice) * 0.02, 1);
  const yMinimum = minimumPrice - priceRange * 0.08;
  const yMaximum = maximumPrice + priceRange * 0.08;
  const timestampMinimum = Math.min(...timestamps);
  const timestampRange = Math.max(Math.max(...timestamps) - timestampMinimum, 1);
  const bounds = {
    width,
    height,
    left: width < 500 ? 62 : 92,
    right: 18,
    top: 18,
    bottom: 42,
  };
  const plotWidth = bounds.width - bounds.left - bounds.right;
  const plotHeight = bounds.height - bounds.top - bounds.bottom;

  const points = validData.map(point => ({
    ...point,
    x: bounds.left + ((point.timestamp - timestampMinimum) / timestampRange) * plotWidth,
    y: bounds.top + ((yMaximum - point.price) / (yMaximum - yMinimum)) * plotHeight,
  }));
  const linePath = points
    .map((point, index) => `${index === 0 ? 'M' : 'L'}${point.x},${point.y}`)
    .join(' ');
  const firstPoint = points[0];
  const lastPoint = points[points.length - 1];
  const chartBottom = bounds.top + plotHeight;

  return {
    bounds,
    points,
    linePath,
    areaPath: `${linePath} L${lastPoint.x},${chartBottom} L${firstPoint.x},${chartBottom} Z`,
    xTicks: getTickIndexes(points.length, width < 500 ? 3 : 5).map(index => points[index]),
    yTicks: Array.from({ length: 5 }, (_, index) => {
      const ratio = index / 4;
      return {
        value: yMaximum - ratio * (yMaximum - yMinimum),
        y: bounds.top + ratio * plotHeight,
      };
    }),
  };
};
