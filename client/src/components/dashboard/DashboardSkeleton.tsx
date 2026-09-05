function Placeholder({ className }: { className: string }) {
  return <div className={`rounded-lg bg-gray-700/60 ${className}`} />;
}

export function DashboardSkeleton() {
  return (
    <div
      className="grid grid-cols-1 gap-6 sm:gap-8 min-w-0 motion-safe:animate-pulse"
      aria-hidden="true"
    >
      <div className="bg-gray-800 rounded-2xl p-4 sm:p-7 shadow-lg border border-gray-700 min-w-0">
        <div className="flex items-center gap-3 sm:gap-4 mb-6">
          <Placeholder className="h-12 w-12 shrink-0 rounded-full" />
          <div className="flex flex-wrap items-center gap-3 min-w-0">
            <Placeholder className="h-7 w-32" />
            <Placeholder className="h-8 w-28" />
          </div>
        </div>
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between mb-6 sm:mb-8 p-4 sm:p-5 bg-gray-900 rounded-xl">
          <div className="sm:w-1/3">
            <Placeholder className="h-5 w-36 mb-1 max-w-full" />
            <Placeholder className="h-10 w-24" />
          </div>
          <Placeholder className="hidden sm:block h-20 w-36 rounded-t-full" />
          <div className="sm:w-1/3 flex sm:justify-end">
            <Placeholder className="h-10 sm:h-12 w-32 rounded-full" />
          </div>
        </div>
        <div className="space-y-3 sm:space-y-5">
          {[0, 1, 2].map(index => (
            <div
              key={index}
              className="flex items-start sm:items-center gap-3 sm:gap-5 bg-gray-900/40 p-3 sm:p-5 rounded-2xl border border-gray-700/50"
            >
              <Placeholder className="h-11 w-11 sm:h-[52px] sm:w-[52px] shrink-0 rounded-xl" />
              <div className="flex-1 min-w-0">
                <Placeholder className="h-6 w-28 max-w-full" />
                <Placeholder className="h-10 sm:h-5 md:h-10 lg:h-5 w-full mt-1" />
              </div>
              <Placeholder className="hidden md:block flex-[2] h-3 mx-2 lg:mx-6 rounded-full" />
              <div className="flex flex-col items-end gap-1.5 shrink-0 w-[58px] sm:w-[70px]">
                <Placeholder className="h-5 sm:h-6 w-12" />
                <Placeholder className="h-6 w-full rounded-full" />
              </div>
            </div>
          ))}
        </div>
      </div>

      <div className="bg-gray-800 rounded-xl p-6 shadow-lg border border-gray-700">
        <div className="flex items-center gap-4 mb-6">
          <Placeholder className="h-11 w-11 shrink-0 rounded-xl" />
          <div className="flex-1 min-w-0">
            <Placeholder className="h-7 w-44 max-w-full" />
            <Placeholder className="h-10 sm:h-5 w-80 max-w-full mt-0.5" />
          </div>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {[0, 1, 2, 3, 4].map(index => (
            <div
              key={index}
              className={`bg-gray-900 p-5 rounded-xl flex items-start gap-4 ${index === 4 ? 'md:col-span-2' : ''}`}
            >
              <Placeholder className="h-12 w-12 shrink-0 rounded-xl" />
              <div className="flex-1 min-w-0">
                <Placeholder className="h-5 w-36 max-w-full mb-1" />
                <Placeholder className="h-8 w-24 max-w-full" />
                <Placeholder className="h-4 w-40 max-w-full mt-1" />
              </div>
            </div>
          ))}
        </div>
      </div>

      <div className="bg-gray-800 rounded-2xl p-4 sm:p-7 shadow-lg border border-gray-700 h-[500px] flex flex-col">
        <div className="flex items-center gap-4 mb-6">
          <Placeholder className="h-[50px] w-[50px] shrink-0 rounded-xl" />
          <div className="flex-1 min-w-0">
            <Placeholder className="h-7 w-48 max-w-full" />
            <Placeholder className="h-10 sm:h-5 w-80 max-w-full mt-0.5" />
          </div>
        </div>
        <div className="flex-1 min-h-0 rounded-lg border-b border-l border-gray-700 flex flex-col justify-between p-4">
          {[0, 1, 2, 3].map(index => (
            <div key={index} className="h-px bg-gray-700/50" />
          ))}
        </div>
        <div className="flex justify-between gap-4 mt-3">
          {[0, 1, 2].map(index => (
            <Placeholder key={index} className="h-4 w-12" />
          ))}
        </div>
      </div>
    </div>
  );
}
