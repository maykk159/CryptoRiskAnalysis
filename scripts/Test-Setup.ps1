#Requires -Version 5.1
# Hermetic integration checks: runs the real wrapper and setup against fake native CLIs.
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$testRoot = Join-Path $repoRoot ('TestResults/setup ' + [Guid]::NewGuid().ToString('N'))
$fixtureRoot = Join-Path $testRoot 'project with spaces'
$toolsRoot = Join-Path $testRoot 'tools'
$savedEnvironment = @{}
foreach ($name in @('PATH', 'SETUP_TEST_LOG', 'SETUP_TEST_NODE', 'SETUP_TEST_DOTNET', 'SETUP_TEST_NPM', 'SETUP_TEST_FAIL')) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$passed = 0

function Assert([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Run-Setup([string[]]$Arguments = @()) {
    Set-Content -LiteralPath $env:SETUP_TEST_LOG -Value ''
    $ErrorActionPreference = 'Continue'
    $output = & (Join-Path $fixtureRoot 'setup.cmd') @Arguments 2>&1
    $code = $LASTEXITCODE
    return [PSCustomObject]@{
        Code = $code
        Output = $output -join "`n"
        Log = Get-Content -Raw -LiteralPath $env:SETUP_TEST_LOG
    }
}

function Test-Case([string]$Name, [scriptblock]$Body) {
    & $Body
    $script:passed++
    Write-Host "[PASS] $Name" -ForegroundColor Green
}

try {
    New-Item -ItemType Directory -Path $fixtureRoot, $toolsRoot -Force | Out-Null
    foreach ($file in @(
        'setup.ps1', 'setup.cmd', 'global.json', 'CryptoRiskAnalysis.API.sln',
        'CryptoRiskAnalysis.API/CryptoRiskAnalysis.API.csproj',
        'CryptoRiskAnalysis.Tests/CryptoRiskAnalysis.Tests.csproj',
        'client/package.json', 'client/package-lock.json', 'client/.npmrc'
    )) {
        $destination = Join-Path $fixtureRoot $file
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $repoRoot $file) -Destination $destination
    }
    Copy-Item -Path (Join-Path $PSScriptRoot 'setup-test-fixtures/*.cmd') -Destination $toolsRoot
    $env:PATH = "$toolsRoot;$PSHOME;$env:SystemRoot\System32;$env:SystemRoot"
    $env:SETUP_TEST_LOG = Join-Path $testRoot 'commands.log'
    $env:SETUP_TEST_NODE = 'v24.19.0'
    $env:SETUP_TEST_DOTNET = '10.0.400'
    $env:SETUP_TEST_NPM = '11.17.0'
    $env:SETUP_TEST_FAIL = ''
    $statePath = Join-Path $fixtureRoot 'client/node_modules/.crypto-risk-setup.json'

    Test-Case 'Check forwards through setup.cmd and does not restore or install' {
        $result = Run-Setup @('-Check')
        Assert ($result.Code -eq 0) $result.Output
        Assert ($result.Log -notmatch 'restore|npm:ci') 'Check changed dependencies.'
        Assert (-not (Test-Path -LiteralPath $statePath)) 'Check wrote install state.'
    }
    Test-Case 'Unsupported .NET fails before restore' {
        $env:SETUP_TEST_DOTNET = '9.0.300'
        $result = Run-Setup
        Assert ($result.Code -ne 0 -and $result.Output -match 'incompatible') $result.Output
        Assert ($result.Log -notmatch 'restore|npm:ci') 'Unsupported SDK changed dependencies.'
        $env:SETUP_TEST_DOTNET = '10.0.400'
    }
    Test-Case 'Node versions below each supported floor, odd releases, and prereleases fail early' {
        foreach ($version in @('v20.19.0', 'v22.22.1', 'v23.0.0', 'v24.14.0', 'v25.0.0', 'v26.0.0-rc.1')) {
            $env:SETUP_TEST_NODE = $version
            $result = Run-Setup @('-Check')
            Assert ($result.Code -ne 0) "Incorrectly accepted $version."
            Assert ($result.Log -notmatch 'restore|npm:ci') 'Node version failure changed dependencies.'
        }
        $env:SETUP_TEST_NODE = 'v24.19.0'
    }
    Test-Case 'Supported Node boundary versions pass' {
        foreach ($version in @('v22.22.2', 'v24.15.0', 'v26.0.0')) {
            $env:SETUP_TEST_NODE = $version
            $result = Run-Setup @('-Check')
            Assert ($result.Code -eq 0) $result.Output
        }
        $env:SETUP_TEST_NODE = 'v24.19.0'
    }
    Test-Case 'Old npm fails before install' {
        $env:SETUP_TEST_NPM = '9.9.0'
        $result = Run-Setup
        Assert ($result.Code -ne 0 -and $result.Log -notmatch 'restore|npm:ci') $result.Output
        $env:SETUP_TEST_NPM = '11.17.0'
    }
    Test-Case 'Missing CLI produces an actionable error' {
        $nodePath = Join-Path $toolsRoot 'node.cmd'
        Move-Item -LiteralPath $nodePath -Destination "$nodePath.disabled"
        try {
            $result = Run-Setup
            Assert ($result.Code -ne 0 -and $result.Output -match 'node was not found on PATH') $result.Output
        }
        finally { Move-Item -LiteralPath "$nodePath.disabled" -Destination $nodePath }
    }
    Test-Case 'Restore failure aborts before npm install and propagates a nonzero exit' {
        $env:SETUP_TEST_FAIL = 'restore'
        $result = Run-Setup
        Assert ($result.Code -ne 0 -and $result.Output -match 'exit 42') $result.Output
        Assert ($result.Log -notmatch 'npm:ci') 'npm ran after restore failed.'
        $env:SETUP_TEST_FAIL = ''
    }
    Test-Case 'Fresh install works from an unrelated working directory with spaces' {
        $before = (Get-FileHash -LiteralPath (Join-Path $fixtureRoot 'client/package-lock.json')).Hash
        Push-Location -LiteralPath $testRoot
        try { $result = Run-Setup } finally { Pop-Location }
        Assert ($result.Code -eq 0) $result.Output
        Assert ($result.Log -match 'dotnet:restore CryptoRiskAnalysis.API.sln') 'Solution was not restored.'
        Assert ($result.Log -match 'npm:ci --include=dev --include=optional --no-audit --no-fund') 'Install flags are wrong.'
        Assert (Test-Path -LiteralPath $statePath) 'Successful install did not record its state.'
        Assert ($before -eq (Get-FileHash -LiteralPath (Join-Path $fixtureRoot 'client/package-lock.json')).Hash) 'Lockfile changed.'
    }
    Test-Case 'Repeat setup validates locally and skips npm ci' {
        $result = Run-Setup
        Assert ($result.Code -eq 0 -and $result.Output -match '\[SKIP\]') $result.Output
        Assert ($result.Log -match 'npm:ls --all' -and $result.Log -notmatch 'npm:ci') $result.Log
    }
    Test-Case 'Force replaces the cached install' {
        $result = Run-Setup @('-Force')
        Assert ($result.Code -eq 0 -and $result.Log -match 'npm:ci') $result.Output
    }
    Test-Case 'Changed lockfile invalidates the cache' {
        Add-Content -LiteralPath (Join-Path $fixtureRoot 'client/package-lock.json') -Value ' '
        $result = Run-Setup
        Assert ($result.Code -eq 0 -and $result.Log -match 'npm:ci') $result.Output
    }
    Test-Case 'Missing CLI shim triggers repair' {
        Remove-Item -LiteralPath (Join-Path $fixtureRoot 'client/node_modules/.bin/vite.cmd')
        $result = Run-Setup
        Assert ($result.Code -eq 0 -and $result.Log -match 'npm:ci') $result.Output
    }
    Test-Case 'Changed manifest, npm config, and installed lockfile invalidate the cache' {
        foreach ($path in @('client/package.json', 'client/.npmrc', 'client/node_modules/.package-lock.json')) {
            Add-Content -LiteralPath (Join-Path $fixtureRoot $path) -Value ' '
            $result = Run-Setup
            Assert ($result.Code -eq 0 -and $result.Log -match 'npm:ci') "Did not reinstall after changing $path. $($result.Output)"
        }
    }
    Test-Case 'Changed Node and npm versions invalidate the cache' {
        $env:SETUP_TEST_NODE = 'v24.20.0'
        $result = Run-Setup
        Assert ($result.Code -eq 0 -and $result.Log -match 'npm:ci') $result.Output
        $env:SETUP_TEST_NPM = '11.18.0'
        $result = Run-Setup
        Assert ($result.Code -eq 0 -and $result.Log -match 'npm:ci') $result.Output
    }
    Test-Case 'Corrupt install state is repaired instead of trusted' {
        Set-Content -LiteralPath $statePath -Value '{broken json'
        $result = Run-Setup
        Assert ($result.Code -eq 0 -and $result.Log -match 'npm:ci') $result.Output
    }
    Test-Case 'Interrupted install cannot retain a successful cache marker' {
        $env:SETUP_TEST_FAIL = 'ci'
        $result = Run-Setup @('-Force')
        Assert ($result.Code -ne 0) $result.Output
        Assert (-not (Test-Path -LiteralPath $statePath)) 'Failed install retained success marker.'
        $env:SETUP_TEST_FAIL = ''
        $result = Run-Setup
        Assert ($result.Code -eq 0 -and $result.Log -match 'npm:ci') $result.Output
    }
    Test-Case 'Dependency tree failure is not reported as successful setup' {
        $env:SETUP_TEST_FAIL = 'ls'
        $result = Run-Setup
        Assert ($result.Code -ne 0 -and $result.Output -match 'incomplete') $result.Output
        Assert (-not (Test-Path -LiteralPath $statePath)) 'Incomplete install has success marker.'
        $env:SETUP_TEST_FAIL = ''
    }
    Test-Case 'Verify runs both frontend checks and the backend Release build/test' {
        $result = Run-Setup @('-Verify')
        Assert ($result.Code -eq 0) $result.Output
        foreach ($expected in @('npm:run format:check', 'npm:run lint', 'npm:run test', 'npm:run build', 'dotnet:build', 'dotnet:test')) {
            Assert ($result.Log.Contains($expected)) "Missing verification: $expected"
        }
    }
    Test-Case 'Verification failure propagates through the wrapper' {
        $env:SETUP_TEST_FAIL = 'lint'
        $result = Run-Setup @('-Verify')
        Assert ($result.Code -ne 0 -and $result.Output -notmatch 'Setup complete') $result.Output
        Assert ($result.Log -notmatch 'npm:run build|dotnet:build') 'Continued verification after failure.'
        $env:SETUP_TEST_FAIL = ''
    }
    Test-Case 'Backend build failure stops before tests' {
        $env:SETUP_TEST_FAIL = 'dotnet-build'
        $result = Run-Setup @('-Verify')
        Assert ($result.Code -ne 0 -and $result.Log -match 'dotnet:build' -and $result.Log -notmatch 'dotnet:test') $result.Output
        $env:SETUP_TEST_FAIL = ''
    }
    Test-Case 'A locked native binary is detected before destructive installation' {
        $binaryPath = Join-Path $fixtureRoot 'client/node_modules/locked.exe'
        Set-Content -LiteralPath $binaryPath -Value 'test'
        $stream = [IO.File]::Open($binaryPath, 'Open', 'ReadWrite', 'None')
        try {
            $result = Run-Setup @('-Force')
            Assert ($result.Code -ne 0 -and $result.Output -match 'Cannot replace') $result.Output
            Assert ($result.Log -notmatch 'npm:ci') 'npm ci ran despite the known file lock.'
        }
        finally { $stream.Dispose() }
    }
    Test-Case 'Conflicting options fail before tool calls' {
        $result = Run-Setup @('-Check', '-Verify')
        Assert ($result.Code -ne 0 -and $result.Log.Trim().Length -eq 0) $result.Output
    }
    Test-Case 'Missing lockfile fails without changing dependencies' {
        Remove-Item -LiteralPath (Join-Path $fixtureRoot 'client/package-lock.json')
        $result = Run-Setup
        Assert ($result.Code -ne 0 -and $result.Output -match 'Required file is missing') $result.Output
        Assert ($result.Log -notmatch 'restore|npm:ci') 'Missing lockfile did not fail early.'
    }
    Write-Host "$passed setup integration checks passed." -ForegroundColor Green
}
finally {
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process')
    }
    if (Test-Path -LiteralPath $testRoot) {
        $resolved = (Resolve-Path -LiteralPath $testRoot).Path
        $allowedRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'TestResults')) + [IO.Path]::DirectorySeparatorChar
        if (-not $resolved.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Refusing to clean a test directory outside TestResults.'
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
