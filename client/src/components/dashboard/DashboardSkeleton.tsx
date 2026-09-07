export function DashboardSkeleton() {
  return (
    <div aria-hidden="true">
      <div className="analysis-grid">
        <div className="panel risk-panel">
          <div className="flex items-center gap-3">
            <div className="skeleton h-[42px] w-[42px]" />
            <div>
              <div className="skeleton h-6 w-32" />
              <div className="skeleton mt-1 h-4 w-36" />
            </div>
          </div>
          <div className="risk-dial skeleton rounded-t-full" />
          <div className="mt-1 flex h-8 items-center justify-between">
            <div className="skeleton h-4 w-10" />
            <div className="skeleton h-7 w-28 rounded-full" />
            <div className="skeleton h-4 w-12" />
          </div>
          <div className="mt-3 flex h-11 items-center border-t border-line lg:hidden">
            <div className="skeleton h-4 w-28" />
          </div>
          <div className="mt-5 hidden space-y-3 border-t border-line pt-4 lg:block">
            {[0, 1, 2].map(index => (
              <div key={index} className="flex gap-3">
                <div className="skeleton h-[34px] w-[34px] shrink-0" />
                <div className="flex-1">
                  <div className="skeleton mb-2 h-[18px] w-full" />
                  <div className="skeleton h-1.5 w-full" />
                </div>
              </div>
            ))}
          </div>
        </div>
        <div className="chart-panel">
          <div className="mb-5 flex items-center gap-3">
            <div className="skeleton h-[42px] w-[42px] shrink-0" />
            <div>
              <div className="skeleton h-6 w-40" />
              <div className="skeleton mt-1 h-[18px] w-40" />
            </div>
          </div>
          <div className="chart-canvas flex flex-col justify-around">
            {[0, 1, 2, 3, 4].map(index => (
              <div key={index} className="h-px bg-line" />
            ))}
          </div>
          <div className="mt-2 flex flex-wrap justify-between gap-1">
            <div className="skeleton h-[18px] w-48" />
            <div className="skeleton h-[18px] w-36" />
          </div>
        </div>
      </div>
      <div className="skeleton mb-3 mt-6 h-6 w-40" />
      <div className="metric-grid">
        {[0, 1, 2, 3, 4].map(index => (
          <div key={index} className="metric-item">
            <div className="skeleton mb-3 h-11 w-11" />
            <div className="skeleton h-5 w-full" />
            <div className="skeleton mt-2 h-9 w-24" />
            <div className="skeleton mt-2 h-[18px] w-full" />
          </div>
        ))}
      </div>
    </div>
  );
}
