import { CheckCircle2, Clock3, RefreshCw, WifiOff, TriangleAlert } from 'lucide-react';

interface DataStatusProps {
  hasData: boolean;
  isFetching: boolean;
  isPaused: boolean;
  hasError: boolean;
  updatedAt: number;
  assetName: string;
  days: number;
}
export function DataStatus({
  hasData,
  isFetching,
  isPaused,
  hasError,
  updatedAt,
  assetName,
  days,
}: DataStatusProps) {
  const label = isPaused
    ? 'Connection paused'
    : isFetching
      ? hasData
        ? 'Refreshing'
        : 'Loading'
      : hasError
        ? hasData
          ? 'Last refresh failed'
          : 'Unable to fetch'
        : hasData
          ? 'Data received'
          : 'Waiting for data';
  const Icon = isPaused
    ? WifiOff
    : isFetching
      ? RefreshCw
      : hasError
        ? TriangleAlert
        : hasData
          ? CheckCircle2
          : Clock3;
  const fetched = updatedAt > 0 ? new Date(updatedAt) : null;
  const announcement = isFetching
    ? `${hasData ? 'Refreshing' : 'Loading'} ${days}-day risk analysis for ${assetName}.`
    : hasData && !hasError
      ? `${days}-day risk analysis for ${assetName} is ready.`
      : '';
  return (
    <div className="max-w-[150px] text-right text-xs sm:max-w-none">
      <p
        role="status"
        aria-atomic="true"
        className={`flex items-center justify-end gap-2 ${hasError || isPaused ? 'text-warning' : 'text-secondary'}`}
      >
        <Icon
          size={14}
          aria-hidden="true"
          className={isFetching ? 'motion-safe:animate-spin' : ''}
        />
        <span>
          {label}
          {announcement ? <span className="sr-only"> · {announcement}</span> : null}
        </span>
      </p>
      <p className="mt-1 text-muted">
        {fetched ? (
          <>
            Last fetched{' '}
            <time dateTime={fetched.toISOString()}>
              {fetched.toLocaleString('en-US', {
                month: 'short',
                day: 'numeric',
                hour: '2-digit',
                minute: '2-digit',
                second: '2-digit',
                hour12: false,
                timeZone: 'UTC',
              })}{' '}
              UTC
            </time>
          </>
        ) : (
          'No successful fetch for this selection'
        )}
      </p>
    </div>
  );
}
