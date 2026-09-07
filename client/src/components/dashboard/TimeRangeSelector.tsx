interface TimeRangeSelectorProps {
  value: number;
  onChange: (days: number) => void;
}
export function TimeRangeSelector({ value, onChange }: TimeRangeSelectorProps) {
  return (
    <fieldset className="min-w-0">
      <legend className="field-label">Analysis Period</legend>
      <div className="flex rounded-lg border border-control bg-canvas p-1">
        {[7, 30, 90].map(days => (
          <button
            type="button"
            key={days}
            aria-label={`${days} Days`}
            onClick={() => onChange(days)}
            aria-pressed={value === days}
            className={`min-h-11 min-w-12 rounded-md px-3 text-sm font-medium transition-colors ${value === days ? 'bg-selection text-accent ring-1 ring-accent' : 'text-secondary hover:bg-raised hover:text-ink'}`}
          >
            {days}D
          </button>
        ))}
      </div>
    </fieldset>
  );
}
