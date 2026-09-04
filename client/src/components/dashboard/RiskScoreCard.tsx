import React from 'react';
import clsx from 'clsx';
import { Activity, TrendingUp, BarChart2 } from 'lucide-react';
import { useAnimatedNumber } from '../../hooks/useAnimatedNumber';

// ─── RiskGauge ────────────────────────────────────────────────────────────────

const RiskGauge: React.FC<{ score: number }> = ({ score }) => {
  const animatedScore = useAnimatedNumber(score, 1000);
  // Map 0-100 to -90 to 90 degrees rotation
  const rotation = (animatedScore / 100) * 180 - 90;

  return (
    <div className="relative w-36 h-20 flex items-end justify-center">
      <svg viewBox="0 0 100 55" className="absolute bottom-0 w-full h-full overflow-visible drop-shadow-md">
        {/* Track Background */}
        <path d="M 10 50 A 40 40 0 0 1 90 50" fill="none" stroke="#374151" strokeWidth="10" />

        {/* 5 Colored Segments */}
        <path d="M 10 50 A 40 40 0 0 1 90 50" fill="none" stroke="#22c55e" strokeWidth="10" strokeDasharray="23.5 105" strokeDashoffset="0" />
        <path d="M 10 50 A 40 40 0 0 1 90 50" fill="none" stroke="#a3e635" strokeWidth="10" strokeDasharray="23.5 105" strokeDashoffset="-25.54" />
        <path d="M 10 50 A 40 40 0 0 1 90 50" fill="none" stroke="#facc15" strokeWidth="10" strokeDasharray="23.5 105" strokeDashoffset="-51.08" />
        <path d="M 10 50 A 40 40 0 0 1 90 50" fill="none" stroke="#fb923c" strokeWidth="10" strokeDasharray="23.5 105" strokeDashoffset="-76.62" />
        <path d="M 10 50 A 40 40 0 0 1 90 50" fill="none" stroke="#ef4444" strokeWidth="10" strokeDasharray="23.5 105" strokeDashoffset="-102.16" />

        {/* Needle */}
        <g style={{ transform: `rotate(${rotation}deg)`, transformOrigin: '50px 50px' }}>
          <polygon points="48.5,50 51.5,50 50,14" fill="#f3f4f6" />
          <circle cx="50" cy="50" r="4" fill="#f3f4f6" />
          <circle cx="50" cy="50" r="1.5" fill="#1f2937" />
        </g>
      </svg>
    </div>
  );
};

// ─── Types ────────────────────────────────────────────────────────────────────

interface RiskScoreCardProps {
  data: {
    compositeRiskScore: number;
    volatilityScore: number;
    trendScore: number;
    volumeScore: number;
  };
  asset?: {
    name: string;
    ticker: string;
    icon: string;
  };
  currentPrice?: number;
}

// ─── ScoreBar ────────────────────────────────────────────────────────────────

const colorClasses = {
  purple: { iconBg: 'bg-purple-500/20', iconText: 'text-purple-400', barFill: 'bg-purple-500', barThumb: 'bg-purple-400', text: 'text-purple-400' },
  blue:   { iconBg: 'bg-blue-500/20',   iconText: 'text-blue-400',   barFill: 'bg-blue-500',   barThumb: 'bg-blue-400',   text: 'text-blue-400' },
  orange: { iconBg: 'bg-orange-500/20', iconText: 'text-orange-400', barFill: 'bg-orange-500', barThumb: 'bg-orange-400', text: 'text-orange-400' },
};

const ScoreBar: React.FC<{
  label: string;
  description: string;
  score: number;
  colorKey: keyof typeof colorClasses;
  icon: React.ReactNode;
  delayMs?: number;
}> = ({ label, description, score, colorKey, icon, delayMs = 0 }) => {
  const animatedScore = useAnimatedNumber(score, 2000, delayMs);

  const getLevelText = (s: number) => {
    if (s < 30) return 'Low';
    if (s < 70) return 'Medium';
    return 'High';
  };

  const levelText = getLevelText(score);
  const theme = colorClasses[colorKey];

  return (
    <div className="flex items-center gap-5 bg-gray-900/40 p-5 rounded-2xl border border-gray-700/50">
      {/* Icon */}
      <div className={clsx('p-3.5 rounded-xl shrink-0', theme.iconBg, theme.iconText)}>
        {icon}
      </div>

      {/* Texts */}
      <div className="flex-1 min-w-[160px]">
        <p className="text-white font-semibold text-base">{label}</p>
        <p className="text-gray-400 text-sm mt-1">{description}</p>
      </div>

      {/* Progress Bar */}
      <div className="flex-[2] hidden md:block mx-6">
        <div className="w-full bg-gray-700/50 rounded-full h-3">
          <div
            className={clsx('h-3 rounded-full relative transition-all duration-[2000ms] ease-out', theme.barFill)}
            style={{ width: `${Math.min(100, Math.max(0, animatedScore))}%` }}
          >
            <div className={clsx('absolute right-0 top-1/2 -translate-y-1/2 w-5 h-5 rounded-full shadow-md', theme.barThumb)} />
          </div>
        </div>
      </div>

      {/* Score & Badge */}
      <div className="text-right shrink-0 min-w-[70px] flex flex-col items-end gap-1.5">
        <span className={clsx('text-2xl font-bold leading-none', theme.text)}>{animatedScore.toFixed(1)}</span>
        <span className={clsx('text-xs font-semibold px-3 py-1 rounded-full', theme.iconBg, theme.text)}>
          {levelText}
        </span>
      </div>
    </div>
  );
};

// ─── RiskScoreCard ────────────────────────────────────────────────────────────

export const RiskScoreCard: React.FC<RiskScoreCardProps> = ({ data, asset, currentPrice }) => {
  // ✅ Hook called at the top level — NOT inside JSX (was a Rules of Hooks violation before)
  const animatedCompositeScore = useAnimatedNumber(data.compositeRiskScore, 1000);

  const getRiskLevel = (score: number) => {
    if (score < 30) return { text: 'Low Risk',    color: 'text-green-400',  bgColor: 'bg-green-400' };
    if (score < 70) return { text: 'Medium Risk', color: 'text-yellow-400', bgColor: 'bg-yellow-400' };
    return             { text: 'High Risk',   color: 'text-red-400',    bgColor: 'bg-red-400' };
  };

  const riskLevel = getRiskLevel(data.compositeRiskScore);

  return (
    <div className="bg-gray-800 rounded-2xl p-7 shadow-lg border border-gray-700">
      <div className="flex items-center gap-4 mb-6">
        {asset && (
          <div className="relative w-12 h-12 shrink-0">
            <img
              src={asset.icon}
              alt={asset.name}
              className="w-12 h-12 rounded-full object-contain bg-white p-1"
              onError={e => {
                (e.target as HTMLImageElement).style.display = 'none';
                (e.target as HTMLImageElement).nextElementSibling?.classList.remove('hidden');
              }}
            />
            <div className="w-12 h-12 rounded-full bg-gray-700 flex items-center justify-center text-lg font-bold text-gray-300 hidden absolute inset-0 border-2 border-gray-600">
              {asset.ticker.substring(0, 2)}
            </div>
          </div>
        )}
        <h2 className="text-xl font-bold text-white flex items-center gap-2 flex-wrap">
          <span>{asset ? asset.name : 'Risk Analysis'}</span>
          {asset && <span className="text-gray-400 text-lg">({asset.ticker})</span>}
          {currentPrice !== undefined && currentPrice !== null && (
            <span className="ml-3 px-3 py-1 text-base font-bold bg-emerald-950/40 text-emerald-400 border border-emerald-500/25 rounded-xl font-mono tracking-tight shadow-inner">
              ${currentPrice.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 8 })}
            </span>
          )}
        </h2>
      </div>

      <div className="flex items-center justify-between mb-8 p-5 bg-gray-900 rounded-xl">
        <div className="w-1/3">
          <p className="text-gray-400 text-sm mb-1">Composite Risk Score</p>
          <p className={`text-4xl font-bold ${riskLevel.color}`}>
            {/* ✅ Using pre-computed variable — was {useAnimatedNumber(...).toFixed(1)} before */}
            {animatedCompositeScore.toFixed(1)}
          </p>
        </div>

        <div className="w-1/3 flex justify-center hidden sm:flex">
          <RiskGauge score={data.compositeRiskScore} />
        </div>

        <div className="w-1/3 flex justify-end">
          <div className={`text-lg font-semibold px-5 py-2.5 rounded-full ${riskLevel.color.replace('text-', 'bg-')} bg-opacity-20`}>
            {riskLevel.text}
          </div>
        </div>
      </div>

      <div className="space-y-5">
        <ScoreBar
          label="Volatility Risk"
          description="Price fluctuation and volatility analysis"
          score={data.volatilityScore}
          colorKey="purple"
          icon={<Activity size={24} />}
          delayMs={0}
        />
        <ScoreBar
          label="Trend Risk"
          description="Market trend and momentum analysis"
          score={data.trendScore}
          colorKey="blue"
          icon={<TrendingUp size={24} />}
          delayMs={150}
        />
        <ScoreBar
          label="Volume Risk"
          description="Trading volume and liquidity analysis"
          score={data.volumeScore}
          colorKey="orange"
          icon={<BarChart2 size={24} />}
          delayMs={300}
        />
      </div>
    </div>
  );
};
