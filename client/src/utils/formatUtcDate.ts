export const formatUtcTime = (timestamp: string | number): string =>
  `${new Date(timestamp).toLocaleTimeString('en-GB', {
    timeZone: 'UTC',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hourCycle: 'h23',
  })} UTC`;

export const formatUtcAxisDate = (timestamp: number): string => {
  const date = new Date(timestamp);
  return `${date.getUTCDate()}/${date.getUTCMonth() + 1}`;
};

export const formatUtcTooltipDate = (timestamp: number): string =>
  new Date(timestamp).toLocaleDateString('en-US', {
    timeZone: 'UTC',
    weekday: 'short',
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
