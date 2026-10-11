interface RiskDialProps {
  score: number;
  valid: boolean;
  color: string;
}

export function RiskDial({ score, valid, color }: RiskDialProps) {
  const angle = (score / 100) * Math.PI;
  return (
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
        {valid && (
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
          className={`text-[52px] font-semibold leading-none tracking-tight tabular-nums ${color}`}
        >
          {valid ? score.toFixed(1) : '—'}
        </p>
        <p className="mt-1 text-xs text-secondary">/100</p>
      </div>
    </div>
  );
}
