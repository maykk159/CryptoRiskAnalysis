import { Activity, Info, Scale, Shield, TrendingDown, type LucideIcon } from 'lucide-react';
import type { RiskAnalysisResponse } from '../../types';
import { formatMetric } from '../../utils/riskPresentation';

type MetricKey =
  | 'downsideRisk'
  | 'maxDrawdown'
  | 'sharpeRatio'
  | 'valueAtRisk95'
  | 'annualizedVolatility';
const METRICS: {
  key: MetricKey;
  label: string;
  caption: string;
  icon: LucideIcon;
  tone: string;
  help: string;
  kind?: 'loss' | 'ratio';
}[] = [
  {
    key: 'downsideRisk',
    icon: TrendingDown,
    tone: 'tone-violet',
    label: 'Downside Risk',
    caption: 'Annualized downside deviation',
    help: 'Annualized downside deviation relative to a 0% daily target. All daily return periods are included.',
  },
  {
    key: 'maxDrawdown',
    icon: TrendingDown,
    tone: 'tone-red',
    label: 'Max Drawdown',
    caption: 'Largest historical decline',
    kind: 'loss',
    help: 'Largest peak-to-trough price decline within the selected historical period.',
  },
  {
    key: 'sharpeRatio',
    icon: Scale,
    tone: 'tone-green',
    label: 'Sharpe Ratio',
    caption: 'Annualized return / risk',
    kind: 'ratio',
    help: 'Annualized risk-adjusted return, using a 0% risk-free return assumption. This is a ratio, not a percentage.',
  },
  {
    key: 'valueAtRisk95',
    icon: Shield,
    tone: 'tone-amber',
    label: 'VaR (95%)',
    caption: 'Estimated daily loss',
    kind: 'loss',
    help: 'Historical daily loss estimate from the 5th percentile of daily returns. It is not a guaranteed maximum loss or a prediction of the worst future outcome.',
  },
  {
    key: 'annualizedVolatility',
    icon: Activity,
    tone: 'tone-blue',
    label: 'Annualized Volatility',
    caption: 'Daily return variability',
    help: 'Standard deviation of historical daily returns, annualized using 365 days.',
  },
];

export function AdvancedMetrics({ data }: { data: Pick<RiskAnalysisResponse, MetricKey> }) {
  return (
    <section aria-labelledby="metrics-heading" className="mt-6">
      <div className="mb-3 flex flex-wrap items-baseline justify-between gap-1">
        <h2 id="metrics-heading" className="section-title">
          Advanced Metrics
        </h2>
        <p className="text-xs text-muted">Based on the selected historical period</p>
      </div>
      <div className="metric-grid">
        {METRICS.map(metric => {
          const Icon = metric.icon;
          const value = data[metric.key];
          const tone = !Number.isFinite(value)
            ? 'tone-muted'
            : metric.key === 'sharpeRatio' && value < 0
              ? 'tone-red'
              : metric.tone;
          return (
            <article key={metric.key} className={`metric-item ${tone}`}>
              <div className="mb-3 flex items-start justify-between gap-2">
                <span className="feature-icon">
                  <Icon size={21} aria-hidden="true" />
                </span>
                <details className="metric-help">
                  <summary aria-label={`About ${metric.label}`}>
                    <Info size={16} aria-hidden="true" />
                  </summary>
                  <p className="metric-help-content">{metric.help}</p>
                </details>
              </div>
              <h3 className="text-[13px] font-medium text-secondary">{metric.label}</h3>
              <p className="metric-value mt-2 break-words text-[30px] font-semibold leading-tight tabular-nums tracking-tight">
                {formatMetric(value, metric.kind)}
              </p>
              <p className="mt-2 text-xs text-muted">{metric.caption}</p>
            </article>
          );
        })}
      </div>
    </section>
  );
}
