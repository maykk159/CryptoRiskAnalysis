# Project requirements and setup

## Prerequisites

| Tool | Supported version | Purpose |
|---|---|---|
| .NET SDK | 10.0.x | API and backend tests |
| Node.js | 20.19+ or 22.12+ | Vite client |
| Git | Current supported release | Source control |

## Install dependencies

From the repository root:

```powershell
dotnet restore CryptoRiskAnalysis.API.sln
cd client
npm.cmd install
cd ..
```

On Windows, run the wrapper below. It applies an execution-policy bypass only to the setup process and does not change the machine or user policy:

```powershell
.\setup.cmd
```

If local PowerShell scripts are already allowed, `./setup.ps1` performs the same setup directly.

## Run

Terminal 1:

```powershell
cd CryptoRiskAnalysis.API
dotnet run
```

Terminal 2:

```powershell
cd client
npm.cmd run dev
```

- API: `http://localhost:5058`
- Client: `http://localhost:5173`

## Verify

```powershell
dotnet build CryptoRiskAnalysis.API.sln -c Release -warnaserror
dotnet test CryptoRiskAnalysis.Tests/CryptoRiskAnalysis.Tests.csproj -c Release --no-build
cd client
npm.cmd run lint
npm.cmd test
npm.cmd run build
```

This repository currently has no Dockerfile or Compose configuration. Use the local .NET and Node.js workflow above.
