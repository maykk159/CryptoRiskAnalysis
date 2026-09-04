# Windows setup script for CryptoRiskAnalysis.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host 'Starting CryptoRiskAnalysis setup...' -ForegroundColor Cyan
Write-Host "`nChecking prerequisites..." -ForegroundColor Yellow

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error '.NET SDK not found. Install .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0'
}
Write-Host '[OK] .NET SDK found' -ForegroundColor Green

if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    Write-Error 'Node.js not found. Install Node.js 20.19+ or 22.12+: https://nodejs.org/'
}

$nodeVersionText = ((& node --version).Trim() -replace '^v', '')
try {
    $nodeVersion = [Version]$nodeVersionText
}
catch {
    Write-Error "Could not parse the installed Node.js version: $nodeVersionText"
}

$isSupportedNodeVersion =
    ($nodeVersion.Major -eq 20 -and $nodeVersion -ge [Version]'20.19.0') -or
    ($nodeVersion -ge [Version]'22.12.0')

if (-not $isSupportedNodeVersion) {
    Write-Error "Node.js $nodeVersion is not supported by Vite 7.3.6. Install Node.js 20.19+ or 22.12+."
}
Write-Host "[OK] Node.js $nodeVersion is supported" -ForegroundColor Green

if (-not (Get-Command npm.cmd -ErrorAction SilentlyContinue)) {
    Write-Error 'npm.cmd was not found. Repair or reinstall Node.js: https://nodejs.org/'
}
Write-Host '[OK] npm.cmd found' -ForegroundColor Green

Write-Host "`nSetting up backend (.NET 10)..." -ForegroundColor Yellow
Push-Location (Join-Path $projectRoot 'CryptoRiskAnalysis.API')
try {
    & dotnet restore
    if ($LASTEXITCODE -ne 0) {
        throw "Backend dependency restore failed with exit code $LASTEXITCODE."
    }
    Write-Host '[OK] Backend dependencies restored' -ForegroundColor Green
}
finally {
    Pop-Location
}

Write-Host "`nSetting up frontend (React)..." -ForegroundColor Yellow
Push-Location (Join-Path $projectRoot 'client')
try {
    & npm.cmd install
    if ($LASTEXITCODE -ne 0) {
        throw "Frontend dependency installation failed with exit code $LASTEXITCODE."
    }
    Write-Host '[OK] Frontend dependencies installed' -ForegroundColor Green
}
finally {
    Pop-Location
}

Write-Host "`nSetup complete. Start the project with:" -ForegroundColor Cyan
Write-Host '1. Backend: cd CryptoRiskAnalysis.API; dotnet run'
Write-Host '2. Frontend: cd client; npm.cmd run dev'
