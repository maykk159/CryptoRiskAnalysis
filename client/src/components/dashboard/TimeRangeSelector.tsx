interface TimeRangeSelectorProps {
  value: number;
  onChange: (days: number) => void;
}

const TIME_RANGES = [
  { days: 7, label: '7 Days' },
  { days: 30, label: '30 Days' },
  { days: 90, label: '90 Days' },
] as const;

export function TimeRangeSelector({ value, onChange }: TimeRangeSelectorProps) {
  return (
    <fieldset className="mb-6">
      <legend className="block text-sm font-medium text-gray-300 mb-2">Analysis Period</legend>
      <div className="flex gap-2">
        {TIME_RANGES.map(({ days, label }) => (
          <button
            type="button"
            key={days}
            onClick={() => onChange(days)}
            aria-pressed={value === days}
            className={`px-4 py-2 rounded-lg font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-300 focus-visible:ring-offset-2 focus-visible:ring-offset-gray-900 ${
              value === days
                ? 'bg-blue-700 text-white hover:bg-blue-600'
                : 'bg-gray-800 text-gray-300 hover:bg-gray-700'
            }`}
          >
            {label}
          </button>
        ))}
      </div>
    </fieldset>
  );
}
