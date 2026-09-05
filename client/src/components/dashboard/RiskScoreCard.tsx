import { memo, type ComponentType } from 'react';
import { Activity, BarChart2, TrendingUp } from 'lucide-react';
import { CryptoAssetIcon } from '../CryptoAssetIcon';
import { useAnimatedNumber } from '../../hooks/useAnimatedNumber';
import type { Asset, RiskAnalysisResponse } from '../../types';

type ScoreKey = 'volatilityScore' | 'trendScore' | 'volumeScore';
type RiskScoreData = Pick<RiskAnalysisResponse, 'compositeRiskScore' | ScoreKey>;

interface RiskScoreCardProps {
  data: RiskScoreData;
  asset?: Asset;
  currentPrice?: number;
}

const GAUGE_SEGMENTS = [
  { color: '#22c55e', offset: 0 },
  { color: '#a3e635', offset: -25.54 },
  { color: '#facc15', offset: -51.08 },
  { color: '#fb923c', offset: -76.62 },
  { color: '#ef4444', offset: -102.16 },
] as const;

const THEMES = {
  purple: {
    icon: 'bg-purple-500/20 text-purple-400',
    fill: 'bg-purple-500',
    thumb: 'bg-purple-400',
    text: 'text-purple-400',
  },
  blue: {
    icon: 'bg-blue-500/20 text-blue-400',
    fill: 'bg-blue-500',
    thumb: 'bg-blue-400',
    text: 'text-blue-400',
  },
  orange: {
    icon: 'bg-orange-500/20 text-orange-400',
    fill: 'bg-orange-500',
    thumb: 'bg-orange-400',
    text: 'text-orange-400',
  },
} as const;

const SCORE_METRICS: Array<{
  key: ScoreKey;
  label: string;
  description: string;
  theme: keyof typeof THEMES;
  icon: ComponentType<{ size?: number }>;
  delay: number;
}> = [
  {
    key: 'volatilityScore',
    label: 'Volatility Risk',
    description: 'Price fluctuation and volatility analysis',
    theme: 'purple',
    icon: Activity,
    delay: 0,
  },
  {
    key: 'trendScore',
    label: 'Trend Risk',
    description: 'Market trend and momentum analysis',
    theme: 'blue',
    icon: TrendingUp,
    delay: 75,
  },
  {
    key: 'volumeScore',
    label: 'Volume Risk',
    description: 'Trading volume and liquidity analysis',
    theme: 'orange',
    icon: BarChart2,
    delay: 150,
  },
];

const getLevelText = (score: number) => {
  if (score < 30) return 'Low';
  if (score < 70) return 'Medium';
  return 'High';
};

const getRiskLevel = (score: number) => {
  if (score < 30) {
    return { text: 'Low Risk', scoreClass: 'text-green-400', badgeClass: 'bg-green-400/20' };
  }
  if (score < 70) {
    return { text: 'Medium Risk', scoreClass: 'text-yellow-400', badgeClass: 'bg-yellow-400/20' };
  }
  return { text: 'High Risk', scoreClass: 'text-red-400', badgeClass: 'bg-red-400/20' };
};

const RiskGauge = memo(function RiskGauge({ score }: { score: number }) {
  const animatedScore = useAnimatedNumber(score, 700);
  const rotation = (animatedScore / 100) * 180 - 90;

  return (
    <div className="relative w-36 h-20 flex items-end justify-center" aria-hidden="true">
      <svg
        viewBox="0 0 100 55"
        className="absolute bottom-0 w-full h-full overflow-visible drop-shadow-md"
      >
        <path d="M 10 50 A 40 40 0 0 1 90 50" fill="none" stroke="#374151" strokeWidth="10" />
        {GAUGE_SEGMENTS.map(segment => (
          <path
            key={segment.color}
            d="M 10 50 A 40 40 0 0 1 90 50"
            fill="none"
            stroke={segment.color}
            strokeWidth="10"
            strokeDasharray="23.5 105"
            strokeDashoffset={segment.offset}
          />
        ))}
        <g style={{ transform: `rotate(${rotation}deg)`, transformOrigin: '50px 50px' }}>
          <polygon points="48.5,50 51.5,50 50,14" fill="#f3f4f6" />
          <circle cx="50" cy="50" r="4" fill="#f3f4f6" />
          <circle cx="50" cy="50" r="1.5" fill="#1f2937" />
        </g>
      </svg>
    </div>
  );
});

const AnimatedCompositeScore = memo(function AnimatedCompositeScore({ score }: { score: number }) {
  return <>{useAnimatedNumber(score, 700).toFixed(1)}</>;
});

const ScoreBar = memo(function ScoreBar({
  label,
  description,
  score,
  themeKey,
  icon: Icon,
  delay,
}: {
  label: string;
  description: string;
  score: number;
  themeKey: keyof typeof THEMES;
  icon: ComponentType<{ size?: number }>;
  delay: number;
}) {
  const animatedScore = useAnimatedNumber(score, 1_500, delay);
  const theme = THEMES[themeKey];

  return (
    <div className="flex items-start sm:items-center gap-3 sm:gap-5 bg-gray-900/40 p-3 sm:p-5 rounded-2xl border border-gray-700/50 min-w-0">
      <div className={`p-2.5 sm:p-3.5 rounded-xl shrink-0 ${theme.icon}`}>
        <Icon size={24} />
      </div>
      <div className="flex-1 min-w-0">
        <p className="text-white font-semibold text-base">{label}</p>
        <p className="text-gray-400 text-sm mt-1">{description}</p>
      </div>
      <div className="flex-[2] hidden md:block mx-2 lg:mx-6 min-w-0">
        <div className="w-full bg-gray-700/50 rounded-full h-3">
          <div
            className={`h-3 rounded-full relative ${theme.fill}`}
            style={{ width: `${Math.min(100, Math.max(0, animatedScore))}%` }}
          >
            <div
              className={`absolute right-0 top-1/2 -translate-y-1/2 w-5 h-5 rounded-full shadow-md ${theme.thumb}`}
            />
          </div>
        </div>
      </div>
      <div className="text-right shrink-0 min-w-[58px] sm:min-w-[70px] flex flex-col items-end gap-1.5">
        <span className={`text-xl sm:text-2xl font-bold leading-none ${theme.text}`}>
          {animatedScore.toFixed(1)}
        </span>
        <span className={`text-xs font-semibold px-2 sm:px-3 py-1 rounded-full ${theme.icon}`}>
          {getLevelText(score)}
        </span>
      </div>
    </div>
  );
});

export function RiskScoreCard({ data, asset, currentPrice }: RiskScoreCardProps) {
  const riskLevel = getRiskLevel(data.compositeRiskScore);

  return (
    <section className="bg-gray-800 rounded-2xl p-4 sm:p-7 shadow-lg border border-gray-700 min-w-0">
      <div className="flex items-start sm:items-center gap-3 sm:gap-4 mb-6 min-w-0">
        {asset && <CryptoAssetIcon asset={asset} size="large" />}
        <h2 className="text-xl font-bold text-white flex items-center gap-2 flex-wrap min-w-0">
          <span>{asset ? asset.name : 'Risk Analysis'}</span>
          {asset && <span className="text-gray-400 text-lg">({asset.ticker})</span>}
          {currentPrice !== undefined && (
            <span className="sm:ml-3 max-w-full px-3 py-1 text-sm sm:text-base font-bold bg-emerald-950/40 text-emerald-400 border border-emerald-500/25 rounded-xl font-mono tracking-tight shadow-inner break-all">
              $
              {currentPrice.toLocaleString(undefined, {
                minimumFractionDigits: 2,
                maximumFractionDigits: 8,
              })}
            </span>
          )}
        </h2>
      </div>

      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between mb-6 sm:mb-8 p-4 sm:p-5 bg-gray-900 rounded-xl min-w-0">
        <div className="w-full sm:w-1/3">
          <p className="text-gray-400 text-sm mb-1">Composite Risk Score</p>
          <p className={`text-4xl font-bold ${riskLevel.scoreClass}`}>
            <AnimatedCompositeScore score={data.compositeRiskScore} />
          </p>
        </div>
        <div className="hidden sm:flex sm:w-1/3 justify-center">
          <RiskGauge score={data.compositeRiskScore} />
        </div>
        <div className="w-full sm:w-1/3 flex justify-start sm:justify-end">
          <div
            className={`text-base sm:text-lg font-semibold px-4 sm:px-5 py-2 sm:py-2.5 rounded-full whitespace-nowrap ${riskLevel.scoreClass} ${riskLevel.badgeClass}`}
          >
            {riskLevel.text}
          </div>
        </div>
      </div>

      <div className="space-y-3 sm:space-y-5">
        {SCORE_METRICS.map(metric => (
          <ScoreBar
            key={metric.key}
            label={metric.label}
            description={metric.description}
            score={data[metric.key]}
            themeKey={metric.theme}
            icon={metric.icon}
            delay={metric.delay}
          />
        ))}
      </div>
    </section>
  );
}
