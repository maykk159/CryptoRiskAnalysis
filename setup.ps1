# Windows Setup Script for CryptoRiskAnalysis
$ErrorActionPreference = "Stop"

Write-Host "🚀 Starting CryptoRiskAnalysis Setup..." -ForegroundColor Cyan

# 1. Check Prerequisites
Write-Host "`n🔍 Checking prerequisites..." -ForegroundColor Yellow

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "❌ .NET SDK not found! Please install .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0"
}
Write-Host "✅ .NET SDK found" -ForegroundColor Green

if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    Write-Error "❌ Node.js not found! Please install Node.js (v18+): https://nodejs.org/"
}
Write-Host "✅ Node.js found" -ForegroundColor Green

# 2. Setup Backend
Write-Host "`n📦 Setting up Backend (.NET 10)..." -ForegroundColor Yellow
Push-Location "CryptoRiskAnalysis.API"
try {
    dotnet restore
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Backend dependencies restored" -ForegroundColor Green
    } else {
        Write-Error "❌ Failed to restore backend dependencies"
    }
}
finally {
    Pop-Location
}

# 3. Setup Frontend
Write-Host "`n🎨 Setting up Frontend (React)..." -ForegroundColor Yellow
Push-Location "client"
try {
    npm install
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Frontend dependencies installed" -ForegroundColor Green
    } else {
        Write-Error "❌ Failed to install frontend dependencies"
    }
}
finally {
    Pop-Location
}

Write-Host "`n✨ Setup Complete! You can now run the project." -ForegroundColor Cyan
Write-Host "1. Backend: cd CryptoRiskAnalysis.API; dotnet run"
Write-Host "2. Frontend: cd client; npm run dev"
Write-Host "`nPress any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
