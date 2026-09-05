import { Activity, LineChart, Scale, Shield, TrendingDown, type LucideIcon } from 'lucide-react';
import type { RiskAnalysisResponse } from '../../types';

type MetricKey =
  | 'downsideRisk'
  | 'maxDrawdown'
  | 'sharpeRatio'
  | 'valueAtRisk95'
  | 'annualizedVolatility';

interface AdvancedMetricsProps {
  data: Pick<RiskAnalysisResponse, MetricKey>;
}

interface MetricDefinition {
  key: MetricKey;
  label: string;
  description: string;
  title: string;
  icon: LucideIcon;
  iconClass: string;
  valueClass: string | ((value: number) => string);
  format: (value: number) => string;
  wide?: boolean;
}

const formatPercentage = (value: number) => `${value.toFixed(2)}%`;
const formatLossPercentage = (value: number) => {
  const magnitude = Math.abs(value).toFixed(2);
  return magnitude === '0.00' ? '0.00%' : `-${magnitude}%`;
};

const METRICS: MetricDefinition[] = [
  {
    key: 'downsideRisk',
    label: 'Downside Risk',
    description: 'Downside volatility only',
    title: 'Volatility of negative returns only - measures downside risk',
    icon: TrendingDown,
    iconClass: 'bg-purple-500/20 text-purple-400',
    valueClass: 'text-white',
    format: formatPercentage,
  },
  {
    key: 'maxDrawdown',
    label: 'Max Drawdown',
    description: 'Worst-case decline',
    title: 'Largest peak-to-trough decline in the period',
    icon: TrendingDown,
    iconClass: 'bg-red-500/20 text-red-400',
    valueClass: 'text-red-400',
    format: formatLossPercentage,
  },
  {
    key: 'sharpeRatio',
    label: 'Sharpe Ratio',
    description: 'Risk-adjusted return',
    title: 'Risk-adjusted return metric - higher is better',
    icon: Scale,
    iconClass: 'bg-emerald-500/20 text-emerald-400',
    valueClass: value =>
      value >= 1 ? 'text-emerald-400' : value >= 0 ? 'text-yellow-400' : 'text-red-400',
    format: value => value.toFixed(2),
  },
  {
    key: 'valueAtRisk95',
    label: 'VaR (95%)',
    description: '95% confidence loss',
    title: '95% confidence worst-case loss',
    icon: Shield,
    iconClass: 'bg-orange-500/20 text-orange-400',
    valueClass: 'text-orange-400',
    format: formatLossPercentage,
  },
  {
    key: 'annualizedVolatility',
    label: 'Annualized Volatility',
    description: 'Historical price volatility',
    title: 'Standard deviation annualized',
    icon: Activity,
    iconClass: 'bg-blue-500/20 text-blue-400',
    valueClass: 'text-blue-400',
    format: formatPercentage,
    wide: true,
  },
];

export function AdvancedMetrics({ data }: AdvancedMetricsProps) {
  return (
    <section className="bg-gray-800 rounded-xl p-6 shadow-lg border border-gray-700">
      <div className="flex items-center gap-4 mb-6">
        <div className="p-2.5 bg-indigo-500/20 rounded-xl text-indigo-400">
          <LineChart size={24} aria-hidden="true" />
        </div>
        <div>
          <h2 className="text-xl font-bold text-white">Advanced Metrics</h2>
          <p className="text-gray-400 text-sm mt-0.5">
            Comprehensive risk and performance analytics
          </p>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {METRICS.map(metric => {
          const value = data[metric.key];
          const valueClass =
            typeof metric.valueClass === 'function' ? metric.valueClass(value) : metric.valueClass;
          const Icon = metric.icon;

          return (
            <div
              key={metric.key}
              className={`bg-gray-900 p-5 rounded-xl flex items-start gap-4 ${
                metric.wide ? 'md:col-span-2' : ''
              }`}
              title={metric.title}
            >
              <div className={`p-3 rounded-xl shrink-0 ${metric.iconClass}`}>
                <Icon size={24} aria-hidden="true" />
              </div>
              <div>
                <p className="text-gray-400 text-sm font-medium mb-1">{metric.label}</p>
                <p className={`text-2xl font-bold ${valueClass}`}>{metric.format(value)}</p>
                <p className="text-gray-400 text-xs mt-1">{metric.description}</p>
              </div>
            </div>
          );
        })}
      </div>
    </section>
  );
}
