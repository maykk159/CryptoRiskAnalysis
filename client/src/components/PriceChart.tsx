import React from 'react';
import {
  AreaChart,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from 'recharts';
import { TrendingUp } from 'lucide-react';
import type { PriceData } from '../types';
import { formatUsdPrice } from '../utils/formatUsdPrice';
import { formatUtcAxisDate, formatUtcTooltipDate } from '../utils/formatUtcDate';

interface PriceChartProps {
  data: PriceData[];
  timeRange: number;
}

export const PriceChart: React.FC<PriceChartProps> = ({ data, timeRange }) => {
  const formattedData = React.useMemo(
    () =>
      data.map(d => {
        const dateObj = new Date(d.timestamp);
        return {
          ...d,
          date: dateObj.toLocaleDateString('en-US', { timeZone: 'UTC' }),
          day: dateObj.getUTCDate(),
          fullDate: dateObj, // keep object if needed
        };
      }),
    [data]
  );

  return (
    <div className="bg-gray-800 rounded-2xl p-7 shadow-lg border border-gray-700 h-[500px] flex flex-col">
      <div className="flex items-center gap-4 mb-6">
        <div className="p-3 bg-indigo-500/10 border border-indigo-500/30 rounded-xl shrink-0 shadow-[0_0_15px_rgba(99,102,241,0.2)]">
          <TrendingUp className="w-6 h-6 text-indigo-400" />
        </div>
        <div>
          <h2 className="text-xl font-bold text-white">{timeRange}-Day Price History</h2>
          <p className="text-gray-400 text-sm mt-0.5">
            Historical price performance over the last {timeRange} days
          </p>
        </div>
      </div>
      <div className="flex-1 min-h-0">
        <ResponsiveContainer width="100%" height="100%">
          <AreaChart data={formattedData}>
            <defs>
              <linearGradient id="colorPrice" x1="0" y1="0" x2="0" y2="1">
                <stop offset="5%" stopColor="#6366f1" stopOpacity={0.4} />
                <stop offset="95%" stopColor="#6366f1" stopOpacity={0} />
              </linearGradient>
            </defs>
            <CartesianGrid strokeDasharray="3 3" stroke="#374151" vertical={false} opacity={0.4} />
            <XAxis
              dataKey="timestamp"
              stroke="#9CA3AF"
              tick={{ fill: '#9CA3AF', fontSize: 12 }}
              tickLine={false}
              axisLine={false}
              tickFormatter={formatUtcAxisDate}
              minTickGap={50}
            />
            <YAxis
              stroke="#9CA3AF"
              tick={{ fill: '#9CA3AF', fontSize: 12 }}
              tickLine={false}
              axisLine={false}
              domain={['auto', 'auto']}
              tickFormatter={formatUsdPrice}
            />
            <Tooltip
              contentStyle={{
                backgroundColor: 'rgba(17, 24, 39, 0.8)',
                borderColor: '#4f46e5',
                borderRadius: '12px',
                color: '#F3F4F6',
                backdropFilter: 'blur(8px)',
                boxShadow: '0 10px 15px -3px rgba(0, 0, 0, 0.5)',
              }}
              itemStyle={{ color: '#818cf8', fontWeight: 'bold' }}
              formatter={(value: number) => [formatUsdPrice(value), 'Price']}
              labelFormatter={formatUtcTooltipDate}
            />
            <Area
              type="monotone"
              dataKey="price"
              stroke="#6366f1"
              strokeWidth={3}
              fillOpacity={1}
              fill="url(#colorPrice)"
              dot={false}
              activeDot={{ r: 6, fill: '#6366f1', stroke: '#c7d2fe', strokeWidth: 3 }}
              animationDuration={3000}
            />
          </AreaChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
};
