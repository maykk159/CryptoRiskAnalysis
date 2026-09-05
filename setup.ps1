#Requires -Version 5.1
<#
.SYNOPSIS
Prepares the local API and client without installing system tools or starting servers.
.PARAMETER Check
Checks repository files and tool versions without restoring or installing packages.
.PARAMETER Force
Reinstalls frontend dependencies from the lockfile, even if the cached install is valid.
.PARAMETER Verify
Also builds and tests the solution and runs frontend formatting, lint, tests, and build.
.EXAMPLE
.\setup.cmd -Verify
#>
[CmdletBinding(DefaultParameterSetName = 'Setup')]
param(
    [Parameter(ParameterSetName = 'Check')]
    [switch]$Check,

    [Parameter(ParameterSetName = 'Setup')]
    [switch]$Force,

    [Parameter(ParameterSetName = 'Setup')]
    [switch]$Verify
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$timer = [Diagnostics.Stopwatch]::StartNew()

function Find-Tool([string]$Name, [string]$HelpText) {
    $command = Get-Command $Name -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if (-not $command) {
        throw "$Name was not found on PATH. $HelpText Open a new terminal after installation."
    }
    return $command.Source
}

function Invoke-Tool {
    param([string]$FilePath, [string[]]$Arguments, [string]$FailureHint)
    # In Windows PowerShell, stderr is an error stream even when a CLI merely logs a warning.
    # Let the native exit code determine whether this step failed.
    $ErrorActionPreference = 'Continue'
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$(Split-Path -Leaf $FilePath) $($Arguments -join ' ') failed (exit $LASTEXITCODE). $FailureHint"
    }
}

function Read-Version([string]$Text, [string]$Name) {
    if ($Text.Trim() -notmatch '^v?(\d+\.\d+\.\d+)$') {
        throw "$Name returned an invalid or prerelease version: $Text"
    }
    return [Version]$Matches[1]
}

function Test-Engine([Version]$Version, [string]$Range) {
    # The project's engines use major-version caret ranges and minimum versions only.
    foreach ($part in ($Range -split '\|\|')) {
        if ($part.Trim() -notmatch '^(\^|>=)(\d+\.\d+\.\d+)$') {
            throw "Unsupported engine range in client/package.json: $Range"
        }
        $operator = $Matches[1]
        $minimum = [Version]$Matches[2]
        if ($Version -ge $minimum -and ($operator -eq '>=' -or $Version.Major -eq $minimum.Major)) {
            return $true
        }
    }
    return $false
}

function Get-InstallFingerprint([string]$NodeVersion, [string]$NpmVersion, [string]$Architecture) {
    $parts = @($NodeVersion, $NpmVersion, $Architecture)
    foreach ($path in @('client/package.json', 'client/package-lock.json', 'client/.npmrc', 'setup.ps1')) {
        $fullPath = Join-Path $projectRoot $path
        if (Test-Path -LiteralPath $fullPath -PathType Leaf) {
            $parts += (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash
        }
    }
    return $parts -join ':'
}

function Test-ClientInstall([string]$NpmPath) {
    foreach ($binary in @('vite', 'tsc', 'vitest', 'eslint', 'prettier')) {
        if (-not (Test-Path -LiteralPath "node_modules/.bin/$binary.cmd" -PathType Leaf)) {
            return $false
        }
    }
    # Validate transitive dependencies too; this is local and makes no registry requests.
    # Continue lets Windows PowerShell capture stderr without treating it as a terminating error.
    $ErrorActionPreference = 'Continue'
    $null = & $NpmPath ls --all --include=dev --include=optional --offline --json 2>&1
    return $LASTEXITCODE -eq 0
}

function Assert-ClientFilesUnlocked {
    if (-not (Test-Path -LiteralPath 'node_modules' -PathType Container)) { return }
    # Windows cannot replace loaded native binaries. Check before npm ci removes any packages.
    $binaries = Get-ChildItem -LiteralPath 'node_modules' -Recurse -File -Include '*.exe', '*.node', '*.dll'
    foreach ($binary in $binaries) {
        try {
            $stream = [IO.File]::Open($binary.FullName, 'Open', 'ReadWrite', 'None')
            $stream.Dispose()
        }
        catch {
            throw "Cannot replace $($binary.FullName). Close this project's Vite/test processes (and any editor holding the file), then rerun setup. No packages have been changed by this install attempt."
        }
    }
}

Push-Location -LiteralPath $projectRoot
try {
    Write-Host 'CryptoRiskAnalysis setup' -ForegroundColor Cyan
    Write-Host '[1/3] Checking repository and prerequisites...'
    foreach ($path in @(
        'global.json', 'CryptoRiskAnalysis.API.sln',
        'CryptoRiskAnalysis.API/CryptoRiskAnalysis.API.csproj',
        'CryptoRiskAnalysis.Tests/CryptoRiskAnalysis.Tests.csproj',
        'client/package.json', 'client/package-lock.json'
    )) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Required file is missing: $path. Restore it from Git and rerun setup."
        }
    }

    $dotnet = Find-Tool 'dotnet' 'Install .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0'
    $node = Find-Tool 'node' 'Install a supported Node.js version: https://nodejs.org/'
    $npm = Find-Tool 'npm.cmd' 'Repair the Node.js installation (including npm): https://nodejs.org/'
    $manifest = Get-Content -Raw -Encoding UTF8 -LiteralPath 'client/package.json' | ConvertFrom-Json
    $sdkConfig = Get-Content -Raw -Encoding UTF8 -LiteralPath 'global.json' | ConvertFrom-Json

    # Resolve from the repository root so global.json, not the caller's directory, selects the SDK.
    $sdkText = Invoke-Tool $dotnet @('--version') 'Install the .NET SDK required by global.json.'
    $sdkVersion = Read-Version ($sdkText -join '') '.NET SDK'
    $sdkMinimum = [Version]$sdkConfig.sdk.version
    if ($sdkVersion -lt $sdkMinimum -or $sdkVersion.Major -ne $sdkMinimum.Major -or
        $sdkVersion.Minor -ne $sdkMinimum.Minor) {
        throw ".NET SDK $sdkVersion is incompatible. Install .NET $($sdkMinimum.Major).$($sdkMinimum.Minor) SDK (minimum $sdkMinimum)."
    }

    $nodeText = Invoke-Tool $node @('--version') 'Repair the Node.js installation.'
    $nodeVersion = Read-Version ($nodeText -join '') 'Node.js'
    if (-not (Test-Engine $nodeVersion $manifest.engines.node)) {
        throw "Node.js $nodeVersion is unsupported. Required: $($manifest.engines.node) (including test tools). Download: https://nodejs.org/"
    }
    $npmText = Invoke-Tool $npm @('--version') 'Repair the npm installation.'
    $npmVersion = Read-Version ($npmText -join '') 'npm'
    if (-not (Test-Engine $npmVersion $manifest.engines.npm)) {
        throw "npm $npmVersion is unsupported. Required: $($manifest.engines.npm)."
    }
    Write-Host "[OK] .NET SDK $sdkVersion | Node.js $nodeVersion | npm $npmVersion" -ForegroundColor Green

    if ($Check) {
        Write-Host 'Prerequisites passed. No packages were installed or restored.' -ForegroundColor Green
        return
    }

    Write-Host '[2/3] Restoring API and test dependencies (incremental)...'
    Invoke-Tool $dotnet @('restore', 'CryptoRiskAnalysis.API.sln', '--nologo') 'Check the NuGet error above and your network connection.'

    Write-Host '[3/3] Preparing frontend dependencies...'
    Push-Location -LiteralPath (Join-Path $projectRoot 'client')
    try {
        $architecture = Invoke-Tool $node @('--print', 'process.arch') 'Could not read the Node.js architecture.'
        $fingerprint = Get-InstallFingerprint $nodeVersion $npmVersion ($architecture -join '')
        $statePath = 'node_modules/.crypto-risk-setup.json'
        $installedLock = 'node_modules/.package-lock.json'
        $isCurrent = $false
        if (-not $Force -and (Test-Path -LiteralPath $statePath) -and (Test-Path -LiteralPath $installedLock)) {
            try {
                $state = Get-Content -Raw -Encoding UTF8 -LiteralPath $statePath | ConvertFrom-Json
                $isCurrent = $state.fingerprint -eq $fingerprint -and
                    $state.installedLockHash -eq (Get-FileHash -LiteralPath $installedLock -Algorithm SHA256).Hash
            }
            catch {
                Write-Host '[INFO] Install state is unreadable; dependencies will be reinstalled.'
            }
        }
        if ($isCurrent) {
            $isCurrent = Test-ClientInstall $npm
        }

        if ($isCurrent) {
            Write-Host '[SKIP] Frontend dependencies are unchanged and the dependency tree is healthy.' -ForegroundColor Green
        }
        else {
            Assert-ClientFilesUnlocked
            Write-Host '[INSTALL] Installing locked dependencies with npm ci (includes development tools).'
            Write-Host 'This replaces client/node_modules. Stop any client dev/test process if Windows reports a file lock.'
            # Invalidate the success marker before any install attempt, including an interrupted one.
            if (Test-Path -LiteralPath $statePath -PathType Leaf) {
                Remove-Item -LiteralPath $statePath
            }
            Invoke-Tool $npm @('ci', '--include=dev', '--include=optional', '--no-audit', '--no-fund') 'Fix the npm error above, then rerun setup. Do not delete package-lock.json.'
            if (-not (Test-ClientInstall $npm) -or -not (Test-Path -LiteralPath $installedLock -PathType Leaf)) {
                throw 'npm completed but the client dependency tree or CLI tools are incomplete. Rerun setup -Force after fixing the npm configuration.'
            }
            @{
                fingerprint = $fingerprint
                installedLockHash = (Get-FileHash -LiteralPath $installedLock -Algorithm SHA256).Hash
            } | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding UTF8
            Write-Host '[OK] Frontend dependencies installed and checked.' -ForegroundColor Green
        }

        if ($Verify) {
            foreach ($script in @('format:check', 'lint', 'test', 'build')) {
                Write-Host "[VERIFY] Frontend: $script"
                Invoke-Tool $npm @('run', $script) "Frontend $script failed; fix the reported errors and rerun setup -Verify."
            }
        }
    }
    finally {
        Pop-Location
    }

    if ($Verify) {
        Write-Host '[VERIFY] Backend: Release build and tests'
        Invoke-Tool $dotnet @('build', 'CryptoRiskAnalysis.API.sln', '-c', 'Release', '--no-restore', '--nologo', '-warnaserror') 'Backend build failed; check the error above.'
        Invoke-Tool $dotnet @('test', 'CryptoRiskAnalysis.Tests/CryptoRiskAnalysis.Tests.csproj', '-c', 'Release', '--no-build', '--no-restore') 'Backend tests failed; check the test output above.'
    }

    Write-Host ("Setup complete in {0:N1}s." -f $timer.Elapsed.TotalSeconds) -ForegroundColor Green
    if (-not $Verify) { Write-Host 'For builds, lint, and tests: .\setup.cmd -Verify' }
    Write-Host "Open two terminals at: $projectRoot"
    Write-Host 'API:    dotnet run --project CryptoRiskAnalysis.API --launch-profile http'
    Write-Host 'Client: npm.cmd --prefix client run dev'
    Write-Host 'URLs:   http://localhost:5058/swagger | http://localhost:5173'
}
catch {
    [Console]::Error.WriteLine("[FAILED] $($_.Exception.Message)")
    exit 1
}
finally {
    Pop-Location
}
