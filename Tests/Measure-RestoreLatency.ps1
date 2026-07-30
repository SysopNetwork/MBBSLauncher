<#
.SYNOPSIS
    Measures how long the launcher takes to reappear after the BBS goes away.

.DESCRIPTION
    End-to-end timing of the user-visible behaviour, using the same trick as
    Soak-Launcher.ps1: stand-in processes named wgserver.exe / wgsappgo.exe (copies
    of ping.exe) in a sandbox folder. The launcher identifies the BBS purely by
    process NAME, so these are indistinguishable from the real thing.

    Per run:
      1. Start the launcher against a sandbox INI (AutoStartBBS OFF, sandbox BBSPath).
      2. Start both stand-ins; wait for the launcher to consider the BBS "up".
      3. Minimize the launcher window (as launching the BBS would).
      4. Kill BOTH stand-ins and start a stopwatch.
      5. Poll IsIconic() every 50ms; stop the clock when the window un-minimizes.

    That elapsed time IS the delay the sysop experiences.

    Safety: aborts if a real wgserver/wgsappgo is running; only ever stops stand-ins
    by the PID it started; uses its own sandbox INI beside a copied exe, so the real
    C:\MBBSLauncher\MBBSLauncher.ini is never touched.

.PARAMETER ExePath  Launcher exe to measure (e.g. a RELEASES\v2.0-betaNN\MBBSLauncher.exe).
.PARAMETER Runs     How many shutdown cycles to time (median is reported).
.PARAMETER Bounce   Instead of a shutdown, kill both stand-ins and bring wgsappgo BACK
                    after this many ms - tests that a mid-cleanup supervisor bounce does
                    NOT leave the launcher sitting in front of the sysop.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Measure-RestoreLatency.ps1 `
        -ExePath "..\RELEASES\v2.0-beta22\MBBSLauncher.exe" -Runs 3

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Measure-RestoreLatency.ps1 `
        -ExePath "..\RELEASES\v2.0-beta22\MBBSLauncher.exe" -Bounce 2500
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [int]$Runs = 3,
    [int]$Bounce = 0,
    [int]$ConfirmMs = 0,      # 0 = leave [Settings] BBSDownConfirmMs unset (use the build's default)
    [int]$RetractSec = -1,    # -1 = leave unset (default 15); 0 disables the retract watch
    [ValidateSet('true', 'false')][string]$MinimizeToTray = 'true'
)

$ErrorActionPreference = 'Stop'

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class W32L {
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
}
"@
$SW_MINIMIZE = 6

# ---------------------------------------------------------------- safety gate
$real = @(Get-Process -Name wgserver, wgsappgo -ErrorAction SilentlyContinue)
if ($real.Count -gt 0) {
    foreach ($p in $real) { Write-Host ("  found: pid={0} name={1} path={2}" -f $p.Id, $p.ProcessName, $p.Path) }
    throw "A BBS process is running (wgserver/wgsappgo). Aborting so this test cannot touch it. If these are leftover stand-ins from an interrupted run, stop them and retry."
}

$ExePath = (Resolve-Path $ExePath).Path
$sandbox = Join-Path $env:TEMP ("mbbsl-latency-" + [IO.Path]::GetRandomFileName().Substring(0, 6))
$bbsDir  = Join-Path $sandbox 'BBSV10'
$appDir  = Join-Path $sandbox 'app'
New-Item -ItemType Directory -Path $bbsDir, $appDir -Force | Out-Null

# Stand-ins: copies of ping.exe. "ping -t localhost" runs until stopped.
$pingSrc = Join-Path $env:SystemRoot 'System32\ping.exe'
$fakeSrv = Join-Path $bbsDir 'wgserver.exe'
$fakeSup = Join-Path $bbsDir 'wgsappgo.exe'
Copy-Item $pingSrc $fakeSrv
Copy-Item $pingSrc $fakeSup

# Sandbox launcher copy + INI (the launcher reads MBBSLauncher.ini beside its own exe).
$testExe = Join-Path $appDir 'MBBSLauncher.exe'
Copy-Item $ExePath $testExe

# ConfigVersion MUST be present: without it ConfigMigration.NeedsMigration() returns true and
# the launcher opens a modal "Configuration Upgrade" box at startup, which blocks every timer
# and makes the whole test silently measure nothing.
$tuning = ''
if ($ConfirmMs -gt 0)   { $tuning += "BBSDownConfirmMs=$ConfirmMs`r`n" }
if ($RetractSec -ge 0)  { $tuning += "BBSRetractWindowSec=$RetractSec`r`n" }

@"
[Paths]
BBSPath=$bbsDir

[Settings]
ConfigVersion=v2.0-test
$tuning
AutoStartBBS=false
AutoLaunchAtStartup=false
MinimizeToTray=$MinimizeToTray
ShowTrayIcon=true
QuietMode=false

[Monitoring]
Enabled=false

[AppManager]
AutoShow=false

[Programs]
Option5=$fakeSup
Option5Name=Go!
"@ | Set-Content -Path (Join-Path $appDir 'MBBSLauncher.ini') -Encoding ASCII

$standIns = @()
function Start-StandIn([string]$exe) {
    $p = Start-Process -FilePath $exe -ArgumentList '-t', '127.0.0.1' -WindowStyle Hidden -PassThru
    $script:standIns += $p
    return $p
}
function Stop-StandIn($p) {
    if ($p -and -not $p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
}

$launcher = $null
$results = @()
try {
    Write-Host "Launcher : $ExePath"
    Write-Host "Sandbox  : $sandbox`n"

    $launcher = Start-Process -FilePath $testExe -PassThru
    Start-Sleep -Seconds 4
    $launcher.Refresh()
    if ($launcher.HasExited) { throw "Launcher exited immediately (exit $($launcher.ExitCode))." }
    $hwnd = $launcher.MainWindowHandle
    if ($hwnd -eq [IntPtr]::Zero) { throw "Launcher has no main window." }

    # A modal dialog (config migration, missing path, ...) blocks the message pump and every
    # timer with it - the test would then measure a frozen launcher and report a false result.
    $title = $launcher.MainWindowTitle
    if ($title -notmatch 'MBBSLauncher') {
        throw "Launcher's main window is '$title' - looks like a modal dialog is blocking startup."
    }

    for ($i = 1; $i -le $Runs; $i++) {
        # --- BBS "up": both processes present, launcher sees Running
        $srv = Start-StandIn $fakeSrv
        $sup = Start-StandIn $fakeSup
        Start-Sleep -Seconds 4          # let the App Manager latch Running

        # --- launcher out of the way, exactly as launching the BBS leaves it
        [void][W32L]::ShowWindow($hwnd, $SW_MINIMIZE)
        Start-Sleep -Milliseconds 700
        if (-not [W32L]::IsIconic($hwnd)) { throw "Could not minimize the launcher window." }

        # --- BBS goes away; clock starts here
        $sw = [Diagnostics.Stopwatch]::StartNew()
        Stop-StandIn $srv
        Stop-StandIn $sup

        $bounced = $false
        $restoredMs = $null
        $deadline = if ($Bounce -gt 0) { $Bounce + 6000 } else { 20000 }
        while ($sw.ElapsedMilliseconds -lt $deadline) {
            if ($Bounce -gt 0 -and -not $bounced -and $sw.ElapsedMilliseconds -ge $Bounce) {
                $sup = Start-StandIn $fakeSup     # supervisor comes back = it was a cleanup
                $bounced = $true
            }
            if ($null -eq $restoredMs -and -not [W32L]::IsIconic($hwnd)) {
                $restoredMs = $sw.ElapsedMilliseconds
                if ($Bounce -le 0) { break }      # shutdown case: that is the number we want
            }
            Start-Sleep -Milliseconds 50
        }

        if ($Bounce -gt 0) {
            # Give the retract watch time to act, then see where the window ended up.
            # "Put away" means minimized OR hidden - IsIconic() alone is false for a hidden window,
            # which would misreport a successful Hide() retract as a failure.
            Start-Sleep -Seconds 3
            $iconic  = [W32L]::IsIconic($hwnd)
            $visible = [W32L]::IsWindowVisible($hwnd)
            $stillUp = (-not $iconic) -and $visible
            $verdict = if ($null -eq $restoredMs) { 'never popped up (debounce held)' }
                       elseif ($stillUp)          { 'POPPED UP AND STAYED UP  <-- bad' }
                       elseif ($iconic)           { "popped up at ${restoredMs}ms, retracted (minimized)" }
                       else                       { "popped up at ${restoredMs}ms, retracted (hidden)" }
            Write-Host ("run {0}: bounce {1}ms -> {2}  [iconic={3} visible={4}]" -f $i, $Bounce, $verdict, $iconic, $visible)
            $results += [pscustomobject]@{ Run = $i; RestoredMs = $restoredMs; StillUp = $stillUp }
            Stop-StandIn $sup
        }
        else {
            if ($null -eq $restoredMs) { Write-Host "run ${i}: NO RESTORE within 20s  <-- bad" }
            else { Write-Host ("run {0}: restored after {1} ms" -f $i, $restoredMs) }
            $results += [pscustomobject]@{ Run = $i; RestoredMs = $restoredMs; StillUp = $null }
        }

        # Put the launcher back where the next run expects it (visible, not minimized).
        [void][W32L]::ShowWindow($hwnd, 9)  # SW_RESTORE
        Start-Sleep -Seconds 2
    }

    $times = $results | Where-Object { $null -ne $_.RestoredMs } | ForEach-Object { $_.RestoredMs }
    if ($Bounce -le 0 -and $times.Count -gt 0) {
        $sorted = $times | Sort-Object
        $median = $sorted[[int]($sorted.Count / 2)]
        Write-Host ("`nRESTORE LATENCY  min={0}ms  median={1}ms  max={2}ms  (n={3})" -f `
            ($sorted | Select-Object -First 1), $median, ($sorted | Select-Object -Last 1), $sorted.Count)
    }
}
finally {
    foreach ($p in $standIns) { Stop-StandIn $p }
    if ($launcher -and -not $launcher.HasExited) { Stop-Process -Id $launcher.Id -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Milliseconds 500
    $auditLog = Join-Path $appDir 'audit.log'
    if (Test-Path $auditLog) {
        Write-Host "`n--- audit.log (BBSDown / Retract lines) ---"
        Select-String -Path $auditLog -Pattern 'BBSDown|Retract' | ForEach-Object { $_.Line }
    }
    Remove-Item $sandbox -Recurse -Force -ErrorAction SilentlyContinue
}

