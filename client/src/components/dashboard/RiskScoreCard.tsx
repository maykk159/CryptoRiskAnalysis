import { useId, useState } from 'react';
import { Activity, BarChart3, ChevronDown, ShieldCheck, TrendingUp } from 'lucide-react';
import { useAnimatedNumber } from '../../hooks/useAnimatedNumber';
import { useWideLayout } from '../../hooks/useWideLayout';
import type { RiskAnalysisResponse } from '../../types';
import { riskLevel } from '../../utils/riskPresentation';

type ScoreKey = 'volatilityScore' | 'trendScore' | 'volumeScore';
const COMPONENTS = [
  { key: 'volatilityScore', label: 'Volatility risk', icon: Activity, tone: 'tone-violet' },
  { key: 'trendScore', label: 'Trend risk', icon: TrendingUp, tone: 'tone-blue' },
  { key: 'volumeScore', label: 'Volume risk', icon: BarChart3, tone: 'tone-cyan' },
] satisfies { key: ScoreKey; label: string; icon: typeof Activity; tone: string }[];

export function RiskScoreCard({
  data,
}: {
  data: Pick<RiskAnalysisResponse, 'compositeRiskScore' | ScoreKey>;
}) {
  const wide = useWideLayout();
  const [expanded, setExpanded] = useState(false);
  const detailId = useId();
  const score = data.compositeRiskScore;
  const animatedScore = useAnimatedNumber(score);
  const animatedComponents = {
    volatilityScore: useAnimatedNumber(data.volatilityScore),
    trendScore: useAnimatedNumber(data.trendScore),
    volumeScore: useAnimatedNumber(data.volumeScore),
  };
  const level = riskLevel(score);
  const angle = (animatedScore / 100) * Math.PI;
  return (
    <section className="panel risk-panel" aria-labelledby={`${detailId}-heading`}>
      <div className="flex items-center gap-3">
        <span className="feature-icon tone-violet">
          <ShieldCheck size={21} aria-hidden="true" />
        </span>
        <div>
          <h2 id={`${detailId}-heading`} className="section-title">
            Risk overview
          </h2>
          <p className="mt-0.5 text-xs text-muted">Composite risk score</p>
        </div>
      </div>
      <div className="risk-dial">
        <svg viewBox="0 0 240 138" className="h-full w-full" aria-hidden="true">
          <path
            d="M 20 120 A 100 100 0 0 1 220 120"
            fill="none"
            stroke="var(--line)"
            strokeWidth="12"
          />
          <g>
            <path
              d="M 20 120 A 100 100 0 0 1 220 120"
              pathLength="100"
              fill="none"
              stroke="var(--positive)"
              strokeWidth="12"
              strokeDasharray="29 71"
            />
            <path
              d="M 20 120 A 100 100 0 0 1 220 120"
              pathLength="100"
              fill="none"
              stroke="var(--warning)"
              strokeWidth="12"
              strokeDasharray="38 62"
              strokeDashoffset="-31"
            />
            <path
              d="M 20 120 A 100 100 0 0 1 220 120"
              pathLength="100"
              fill="none"
              stroke="var(--negative)"
              strokeWidth="12"
              strokeDasharray="29 71"
              strokeDashoffset="-71"
            />
          </g>
          <path
            d="M 36 120 A 84 84 0 0 1 204 120"
            fill="none"
            stroke="var(--line)"
            strokeWidth="1"
            strokeDasharray="2 6"
          />
          {level.valid && (
            <circle
              cx={120 - 100 * Math.cos(angle)}
              cy={120 - 100 * Math.sin(angle)}
              r="7"
              fill="var(--ink)"
              stroke="var(--panel)"
              strokeWidth="4"
            />
          )}
        </svg>
        <div className="absolute inset-x-0 bottom-2 text-center">
          <p
            className={`text-[52px] font-semibold leading-none tracking-tight tabular-nums ${level.color}`}
          >
            {level.valid ? animatedScore.toFixed(1) : '—'}
          </p>
          <p className="mt-1 text-xs text-secondary">/100</p>
        </div>
      </div>
      <div className="flex items-center justify-between text-xs text-muted">
        <span>Low · 0</span>
        <span className={`risk-badge ${level.color}`}>
          <ShieldCheck size={13} aria-hidden="true" />
          {level.label}
        </span>
        <span>100 · High</span>
      </div>
      <button
        type="button"
        className="mt-3 flex min-h-11 w-full items-center justify-between border-t border-line pt-2 text-sm font-medium text-secondary lg:hidden"
        aria-expanded={wide || expanded}
        aria-controls={detailId}
        onClick={() => setExpanded(value => !value)}
      >
        Risk breakdown{' '}
        <ChevronDown size={16} aria-hidden="true" className={expanded ? 'rotate-180' : ''} />
      </button>
      <div
        id={detailId}
        hidden={!wide && !expanded}
        className="mt-3 space-y-3 lg:mt-5 lg:border-t lg:border-line lg:pt-4"
      >
        {COMPONENTS.map(({ key, label, icon: Icon, tone }) => {
          const component = riskLevel(data[key]);
          return (
            <div key={key} className="flex items-center gap-3">
              <span className={`component-icon ${tone}`}>
                <Icon size={17} aria-hidden="true" />
              </span>
              <div className="min-w-0 flex-1">
                <div className="mb-2 flex flex-wrap items-baseline justify-between gap-1 text-xs">
                  <span className="text-secondary">{label}</span>
                  <span className={`font-semibold tabular-nums ${component.color}`}>
                    {component.valid ? animatedComponents[key].toFixed(1) : '—'}
                    <span className="font-normal text-muted"> /100</span>
                    <span className="sr-only"> · {component.label}</span>
                  </span>
                </div>
                <div className="h-1.5 rounded-full bg-line" aria-hidden="true">
                  <div
                    className={`h-full rounded-full ${component.fill}`}
                    style={{ width: `${component.valid ? animatedComponents[key] : 0}%` }}
                  />
                </div>
              </div>
            </div>
          );
        })}
      </div>
    </section>
  );
}
