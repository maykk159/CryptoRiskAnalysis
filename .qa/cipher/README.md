# CIPHER frontend — implementation and QA

Date: 2026-09-07. UI copy remains English. Backend calculations, endpoint contract, packages and launch scripts are unchanged. No deployment or commit.

## Delivered

- Shared dark palette and surface tokens in index.css / tailwind.config.js.
- Compact header and analysis toolbar; locally filtered editable asset combobox with keyboard navigation and selected/active option relationships.
- Dedicated currentPrice summary, no historical-price fallback. Fixed en-US formatting and small-price precision.
- Desktop chart / risk overview in an 8:4 layout; compact mobile overview with accessible risk breakdown.
- All three component scores and five advanced metrics retained. Keyboard/touch accessible metric explanations match the backend formulas.
- TanStack query keys, AbortSignal, polling, stale time, focus refresh and retry policy retained. One manual refresh for the selected query.
- Successful fetch timestamps, loading, background refresh, failure and paused/offline states. Failed refresh preserves same-query data and successful fetch time.
- SVG chart uses the existing chart model, UTC dates, unique gradient IDs, bounded tooltips, keyboard and touch inspection. Historical changes clear obsolete active points.
- No number-tween or chart draw animation. Reduced-motion preference disables remaining transitions and loading animations.

## Automated checks

| Command (client/) | Result |
| --- | --- |
| npm.cmd run test | PASS — 38 tests, 6 files |
| npm.cmd run lint | PASS |
| npm.cmd run build | PASS — TypeScript + Vite |
| npm.cmd run format:check | PASS |
| git diff --check | PASS |

Tests cover combobox filtering and keyboard editing, no results, currentPrice versus history, tiny/invalid prices, retained data/timestamps after failure, rapid selection and aborted requests, parallel-refresh prevention, UTC chart behavior, flat/single/invalid series, risk thresholds and loss/ratio formatting.

## Browser evidence

Installed Edge was driven through the already installed Playwright runtime. No browser/test dependency was added to client/package.json. The preferred CUA runtime failed to start due to a Windows sandbox helper error; CLI execution with reviewed escalation enabled the alternative browser workflow.

Successful Bitcoin, SHIB and Internet Computer views used the running local API. Error/loading/empty/offline scenarios used browser-only network interception and a captured API response; fixtures are confined to this QA directory.

All requested viewport sizes were captured and visually inspected. Automated document-width checks found no horizontal overflow.

- [1440×900 desktop](bitcoin-1440.png)
- [1366×768 desktop](bitcoin-1366.png)
- [768×1024 tablet](bitcoin-768.png)
- [390×844 mobile](bitcoin-390.png)
- [360×800 mobile](bitcoin-360.png)
- [Small-price coin](shib-360.png)
- [Long-name coin](long-name-360.png)
- [Open asset search](search-360.png)
- [No matching assets](no-assets-360.png)
- [Initial loading](loading-360.png)
- [Initial HTTP 429](initial-429-360.png)
- [Refresh error, desktop](refresh-error-1366.png)
- [Refresh error, mobile](refresh-error-360.png)
- [Offline / paused request](offline-360.png)
- [Empty chart](empty-chart-390.png)
- [First point, keyboard focus](keyboard-first-1440.png)
- [Last point, reduced motion](keyboard-last-reduced-motion-1440.png)
- [Touch inspection](touch-last-360.png)
- [Expanded mobile risk details](risk-expanded-360.png)
- [Metric explanation](metric-help-1440.png)
- [200% CSS scaling](zoom-200-1440.png)
- [200% equivalent reflow](zoom-200-reflow-1440.png)

Structured results: [browser-results.json](browser-results.json), [states-results.json](states-results.json), [touch-results.json](touch-results.json).
No page JavaScript errors or React warnings were recorded. Expected HTTP/network errors from simulated failures are not application errors. Existing remote icon loading can fail; fallback initials retain dimensions.

Touch gestures moved the page vertically from scrollY 571 to 706 while originating on the chart. First/last tooltips stayed within the chart card. Reduced-motion check found no running animation.

## Contrast

Measured using WCAG relative luminance on the final palette:
- Minimum normal text contrast across the used neutral surfaces: 5.52:1.
- Accent on selected surface: 5.91:1.
- Control border on raised surface: 3.28:1; on main panel: 3.68:1.
- Control border adjusted from #50627D to #607493 to reach the target.
- Low-contrast separators are decorative, not the only indicator of a control or selection.

This is targeted visual/keyboard QA, not an exhaustive assistive-technology certification.

## Specific validation limit

Browser-menu zoom itself could not be automated in the isolated headless Edge session. 200% CSS scaling and equivalent reflow (720 CSS-pixel viewport with deviceScaleFactor 2 for a 1440-pixel display) were captured and inspected without overflow. A native browser-menu 200% check remains unverified.

## Run

The existing API is still used at http://localhost:5058.
The frontend is running at http://localhost:5173 (started for this task).
Only owned test browser processes were closed. No existing service was terminated.

If restarting is needed, from the repository root in two terminals:

    cd CryptoRiskAnalysis.API
    dotnet run

    cd client
    npm.cmd run dev
## Visual revision after user feedback

The quieter first redesign was revised in response to the user's request for a more expressive, colorful interface. The current screenshots reflect this revision:
- Color-coded metric values and symbols, tinted metric surfaces and colored top borders.
- A compact semicircular composite-risk dial, color-matched score and risk badge.
- Colorful component icons with actual risk-colored values and proportional bars.
- A framed asset/price surface, stronger branding, chart header and area fill.
- Mobile header compaction and matching skeleton geometry.
- Metric explanations open from named, keyboard/touch-operable information controls without stretching adjacent cards.

All 38 tests, lint, production build and format check passed again after this revision.
The five viewport screenshots and browser state scenarios were regenerated; no runtime JS errors or React warnings were recorded.
Touch scrolling was rechecked (scrollY 675 to 812).
New gradients and tint surfaces retain normal-text contrast above 4.5:1; the previous contrast table describes the original base tokens rather than every new gradient sample.
The existing browser-menu zoom validation limitation still applies.
