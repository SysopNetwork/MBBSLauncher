# Test-ModuleVersionCheck.ps1
# Validates SNTICKLR module version-check behavior for MBBSLauncher v2.0-beta9+
#
# Prerequisites:
#   - MBBSLauncher.exe deployed to $LauncherDir
#   - wgserver must NOT be running (version update tests require BBS to be down)
#   - MBBSLauncher.ini in $LauncherDir must have [Paths] BBSPath=$BBSDir
#     and [Monitoring] Enabled=true  (the script sets these automatically)
#
# Usage:
#   .\Test-ModuleVersionCheck.ps1                  # run all 4 scenarios
#   .\Test-ModuleVersionCheck.ps1 -Scenario 3      # run one scenario
#
# After each scenario setup the script pauses and asks you to launch the exe manually,
# then verifies the result when you press Enter.

param(
    [string]$LauncherDir = "C:\mbbslauncher",
    [string]$BBSDir      = "C:\bbsv10",
    [ValidateSet("1","2","3","4","all")]
    [string]$Scenario    = "all"
)

$DllPath     = "$BBSDir\SNTICKLR.DLL"
$MdfPath     = "$BBSDir\SNTICKLR.MDF"
$IniPath     = "$LauncherDir\MBBSLauncher.ini"
$LauncherExe = "$LauncherDir\MBBSLauncher.exe"
$ExpectedVer = "1.0.0.0"   # matches TicklerMonitor.ModuleShipVersion

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

function Assert-Prerequisites {
    if (-not (Test-Path $LauncherExe)) {
        Write-Error "Launcher not found: $LauncherExe"
        exit 1
    }
    if (-not (Test-Path $BBSDir)) {
        Write-Error "BBS directory not found: $BBSDir  (create it or adjust -BBSDir)"
        exit 1
    }
    if (Get-Process "wgserver" -ErrorAction SilentlyContinue) {
        Write-Warning "wgserver is running. All four version-check scenarios require the BBS to be stopped first."
        Write-Warning "Stop the BBS and re-run this script."
        exit 1
    }
    Write-Host "Prerequisites OK." -ForegroundColor Green
}

function Get-InstalledDllVersion {
    if (-not (Test-Path $DllPath)) { return $null }
    try {
        $vi = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($DllPath)
        if ($vi.FileVersion) { return $vi.FileVersion }
        return "(no version resource)"
    } catch {
        return "(unreadable — likely not a PE file)"
    }
}

# Simple INI key setter — handles existing key or adds it under the section.
function Set-IniKey {
    param([string]$FilePath, [string]$Section, [string]$Key, [string]$Value)

    $lines = if (Test-Path $FilePath) { [System.IO.File]::ReadAllLines($FilePath) } else { @() }
    $out   = [System.Collections.Generic.List[string]]::new()
    $inSec = $false
    $keySet = $false
    $secFound = $false

    foreach ($line in $lines) {
        if ($line -match "^\[$Section\]\s*$") {
            $inSec = $true; $secFound = $true
            $out.Add($line); continue
        }
        if ($line -match "^\[" -and $inSec) {
            if (-not $keySet) { $out.Add("$Key=$Value"); $keySet = $true }
            $inSec = $false
        }
        if ($inSec -and $line -match "^$Key\s*=") {
            $out.Add("$Key=$Value"); $keySet = $true; continue
        }
        $out.Add($line)
    }
    if ($inSec -and -not $keySet) { $out.Add("$Key=$Value") }
    if (-not $secFound) { $out.Add(""); $out.Add("[$Section]"); $out.Add("$Key=$Value") }

    [System.IO.File]::WriteAllLines($FilePath, $out)
}

function Enable-MonitoringConfig {
    Set-IniKey $IniPath "Paths"      "BBSPath" $BBSDir
    Set-IniKey $IniPath "Monitoring" "Enabled" "true"
    Write-Host "  INI: BBSPath=$BBSDir, Monitoring.Enabled=true" -ForegroundColor DarkGray
}

function Backup-Files {
    if (Test-Path $DllPath) { Copy-Item $DllPath "$DllPath.bak" -Force }
    if (Test-Path $MdfPath) { Copy-Item $MdfPath "$MdfPath.bak" -Force }
}

function Restore-Files {
    Remove-Item $DllPath -ErrorAction SilentlyContinue
    Remove-Item $MdfPath -ErrorAction SilentlyContinue
    if (Test-Path "$DllPath.bak") { Move-Item "$DllPath.bak" $DllPath -Force }
    if (Test-Path "$MdfPath.bak") { Move-Item "$MdfPath.bak" $MdfPath -Force }
    Write-Host "  Original SNTICKLR files restored." -ForegroundColor DarkGray
}

function Wait-AndVerify {
    param([string]$Expected, [string]$PassMsg, [string]$FailMsg)
    Write-Host ""
    Write-Host ">>> Launch  $LauncherExe  then close it, and press Enter here to verify. <<<" -ForegroundColor Green
    Read-Host | Out-Null

    $after = Get-InstalledDllVersion
    Write-Host "  DLL version after: $(if ($after) { $after } else { 'NOT PRESENT' })" -ForegroundColor Yellow

    if ($Expected -eq "__unchanged__") {
        # Caller will check manually
        return $after
    }

    if ($after -eq $Expected) {
        Write-Host "  PASS: $PassMsg" -ForegroundColor Green
    } else {
        Write-Host "  FAIL: $FailMsg (got: $after)" -ForegroundColor Red
    }
    return $after
}

# ---------------------------------------------------------------------------
# Scenarios
# ---------------------------------------------------------------------------

function Run-Scenario1 {
    Write-Host ""
    Write-Host "=== Scenario 1: Missing Files ===" -ForegroundColor Cyan
    Write-Host "    SNTICKLR.DLL and .MDF are absent. Launcher should install them from the embedded resource."
    Backup-Files
    Remove-Item $DllPath -ErrorAction SilentlyContinue
    Remove-Item $MdfPath -ErrorAction SilentlyContinue
    Enable-MonitoringConfig

    Write-Host "  DLL version before: NOT PRESENT" -ForegroundColor Yellow
    Wait-AndVerify `
        -Expected $ExpectedVer `
        -PassMsg  "Files were installed. Version = $ExpectedVer." `
        -FailMsg  "Expected $ExpectedVer"
    Restore-Files
}

function Run-Scenario2 {
    Write-Host ""
    Write-Host "=== Scenario 2: Correct Version Already Installed ===" -ForegroundColor Cyan
    Write-Host "    Launcher should detect the version is current and do nothing."

    $cur = Get-InstalledDllVersion
    if ($cur -ne $ExpectedVer) {
        Write-Host "  WARNING: Current version is '$cur', not $ExpectedVer." -ForegroundColor Yellow
        Write-Host "           Run Scenario 1 first so the correct version is installed, then re-run Scenario 2."
        return
    }

    Enable-MonitoringConfig
    Write-Host "  DLL version before: $cur" -ForegroundColor Yellow

    $before = $cur
    $after  = Wait-AndVerify -Expected "__unchanged__" -PassMsg "" -FailMsg ""

    if ($after -eq $before) {
        Write-Host "  PASS: Version unchanged ($after). Launcher made no changes." -ForegroundColor Green
    } else {
        Write-Host "  FAIL: Version changed from $before to $after (should have been a no-op)." -ForegroundColor Red
    }
}

function Run-Scenario3 {
    Write-Host ""
    Write-Host "=== Scenario 3: Old / Unversioned Module Installed ===" -ForegroundColor Cyan
    Write-Host "    A zero-byte file has no PE header so GetVersionInfo returns 0.0.0.0."
    Write-Host "    Launcher should replace it with the embedded $ExpectedVer."
    Backup-Files

    # Zero-byte file — FileVersionInfo.GetVersionInfo throws, treated as Version(0,0,0,0) < ship.
    [System.IO.File]::WriteAllBytes($DllPath, [byte[]]@())
    [System.IO.File]::WriteAllBytes($MdfPath, [byte[]]@())
    Enable-MonitoringConfig

    $before = Get-InstalledDllVersion
    Write-Host "  DLL version before: $(if ($before) { $before } else { '(empty/unreadable)' })" -ForegroundColor Yellow

    Wait-AndVerify `
        -Expected $ExpectedVer `
        -PassMsg  "Old/unversioned module was replaced with $ExpectedVer." `
        -FailMsg  "Expected $ExpectedVer"
    Restore-Files
}

function Run-Scenario4 {
    Write-Host ""
    Write-Host "=== Scenario 4: Newer Version Installed — No Downgrade ===" -ForegroundColor Cyan
    Write-Host "    notepad.exe (version 10.x) is placed as SNTICKLR.DLL to simulate a newer module."
    Write-Host "    Launcher should leave it alone (never downgrade)."
    Backup-Files

    $notepad = "C:\Windows\System32\notepad.exe"
    $npVer   = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($notepad)
    Write-Host "  Using notepad.exe as fake newer DLL (version $($npVer.FileVersion))" -ForegroundColor DarkGray

    Copy-Item $notepad $DllPath -Force
    # MDF just needs to exist
    if (-not (Test-Path $MdfPath)) { [System.IO.File]::WriteAllBytes($MdfPath, [byte[]]@(1)) }
    Enable-MonitoringConfig

    $before = Get-InstalledDllVersion
    Write-Host "  DLL version before: $before" -ForegroundColor Yellow

    $after = Wait-AndVerify -Expected "__unchanged__" -PassMsg "" -FailMsg ""

    if ($after -eq $before) {
        Write-Host "  PASS: Launcher did not downgrade the newer version ($after)." -ForegroundColor Green
    } else {
        Write-Host "  FAIL: Launcher downgraded from $before to $after — no-downgrade rule broken!" -ForegroundColor Red
    }
    Restore-Files
}

# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

Assert-Prerequisites

switch ($Scenario) {
    "1"   { Run-Scenario1 }
    "2"   { Run-Scenario2 }
    "3"   { Run-Scenario3 }
    "4"   { Run-Scenario4 }
    "all" { Run-Scenario1; Run-Scenario2; Run-Scenario3; Run-Scenario4 }
}

Write-Host ""
Write-Host "Done." -ForegroundColor Cyan
