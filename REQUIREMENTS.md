# Project requirements and setup

## Prerequisites

| Tool | Supported version | Purpose |
|---|---|---|
| .NET SDK | Stable 10.0.x, minimum 10.0.100 | API and backend tests; selected through `global.json` |
| Node.js | 22.22.2+ within 22.x, 24.15.0+ within 24.x, or 26+ | Client and test tools; CI uses Node 24 |
| npm | 10+ | Lockfile-based dependency installation |
| PowerShell | 5.1+ on Windows | Setup wrapper and regression tests |
| Git | A supported release | Clone and update the repository |

The frontend requirements are declared in `client/package.json`. `client/.npmrc` also enforces them for manual npm installs. Node 20, 21, 23, and 25 do not meet the project's supported engine ranges.

## Windows setup

From the repository root:

```powershell
.\setup.cmd
```

The wrapper applies an execution-policy bypass only to its own process and forwards all options. It never changes machine/user policy. Administrator rights are not required. After installing system tools, open a new terminal so PATH is refreshed.

| Option | Purpose |
|---|---|
| `-Check` | Check required files and tool versions only; no package install/restore |
| `-Verify` | Also run client format/lint/tests/build and the backend Release build/tests |
| `-Force` | Replace `client/node_modules` using the committed lockfile |

`-Force -Verify` can be combined. `-Check` cannot be combined with either option. If scripts are already permitted, `./setup.ps1` accepts the same options.

Setup restores both .NET projects incrementally and uses `npm ci --include=dev --include=optional --no-audit --no-fund` for reproducible frontend installs, including development tools when `NODE_ENV=production` is set. Audit can be run separately with `npm.cmd --prefix client audit`; installation is not a security audit.

A successful install records a fingerprint inside the ignored `client/node_modules` directory. Setup reinstalls when package metadata, the project `.npmrc`, setup logic, Node/npm version or architecture, or npm's installed lockfile changes. Otherwise it checks CLI shims and the complete local npm dependency tree before skipping the install. No custom NuGet cache or automatic package upgrades are used.

## Manual setup

```powershell
dotnet restore CryptoRiskAnalysis.API.sln
cd client
npm.cmd ci --include=dev --include=optional --no-audit --no-fund
cd ..
```

On macOS/Linux, use `npm` instead of `npm.cmd`.

## Run

Open two terminals at the repository root:

```powershell
# Terminal 1
dotnet run --project CryptoRiskAnalysis.API --launch-profile http
```

```powershell
# Terminal 2
npm.cmd --prefix client run dev
```

- API / Swagger: `http://localhost:5058/swagger`
- Client: `http://localhost:5173`
- No API keys or database setup are required. Existing `.env.local` and appsettings files are preserved.

## Verify

```powershell
.\setup.cmd -Verify
```

This runs the backend Release build with warnings as errors and all backend tests, plus the frontend formatting check, lint, tests, and production build. The first failed step stops setup and returns a nonzero exit code. Without `-Verify`, success means dependencies are ready; it does not claim the application has passed build/tests.

The setup itself has isolated Windows regression checks covering prerequisites, version boundaries, repeated runs, invalidation/repair, file locks, working directories with spaces, option forwarding, and failure exit codes:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Setup.ps1
```

These checks use fake native CLIs and temporary copies under `TestResults`, so they do not alter the real dependencies or require network access. They also run in GitHub Actions.

## Troubleshooting

- **Unsupported SDK/Node/npm:** Install the versions above, reopen the terminal, and rerun `setup.cmd -Check`. A .NET runtime alone cannot build the API.
- **Locked `esbuild.exe`, `.node`, or `.dll`:** Stop this project's frontend dev/test process with `Ctrl+C`, close any editor holding that file, and rerun setup. Setup detects existing native-file locks before `npm ci` starts removing packages and never kills processes automatically.
- **Interrupted or modified install:** Run `setup.cmd -Force`. It replaces only frontend dependencies, not the committed lockfile or local app configuration.
- **Manifest/lockfile mismatch:** Restore the matching `package.json` and `package-lock.json` from Git. If the dependency change was intentional, update the lockfile explicitly with `npm install` and commit both files; setup will not resolve the mismatch by silently changing versions.
- **Network, proxy, or registry errors:** Correct the npm/NuGet configuration and retry. Initial installation needs package registry access; repeated setup reuses local dependencies and NuGet's cache.
- **Build or test failure:** Read the first failed step. Setup never reports completion after a failed native command.

This repository does not contain a Dockerfile or Compose configuration; use the .NET and Node.js workflow above.
