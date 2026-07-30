<#
.SYNOPSIS
    Soak / fuzz harness for MBBSLauncher — exercises open, close, minimize-to-tray,
    window move/resize, and simulated BBS shutdown / cleanup / crash cycles to shake
    out bugs (exceptions, hangs, GDI/handle leaks, stray error dialogs).

.DESCRIPTION
    Safe by design:
      * Uses a SANDBOX BBS folder (not your real C:\BBSV10) with AutoStartBBS OFF,
        so the launcher can never accidentally start the real BBS.
      * Simulates "BBS up / cleanup / shutdown / crash" with harmless stand-in
        processes named wgserver.exe / wgsappgo.exe (copies of ping.exe). The
        launcher detects the BBS purely by process NAME, so these look identical
        to the real thing.
      * Only ever stops stand-in processes by the PID it started — never by name —
        so it cannot touch a real BBS process.
      * Aborts up front if the real BBS (from C:\BBSV10) is already running.
      * Backs up your real MBBSLauncher.ini and restores it on exit.

    Bug signals collected:
      * New "Exception:" / error lines in audit.log (the app's global handlers).
      * Unexpected launcher exit (crash) when it should still be running.
      * Stray modal dialogs owned by the launcher (title + text captured).
      * GDI object / USER object / handle counts sampled each cycle (leak trend).

.PARAMETER Cycles     Number of soak cycles against a single running instance.
.PARAMETER Relaunches Number of full open->exercise->close churn rounds.
.PARAMETER DriveKeys  Also send F12 (open config) / ESC to exercise the config editor.
.PARAMETER Seed       RNG seed for reproducible runs.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Soak-Launcher.ps1 -Cycles 40 -Relaunches 5
#>
[CmdletBinding()]
param(
    [string]$ExePath   = "C:\MBBSLauncher\MBBSLauncher.exe",
    [string]$RealBbs   = "C:\BBSV10",
    [int]$Cycles       = 30,
    [int]$Relaunches   = 4,
    [switch]$DriveKeys,
    [int]$Seed         = 12345
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms

# ----------------------------------------------------------------------------
# Win32 interop
# ----------------------------------------------------------------------------
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class W32 {
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int ht, bool repaint);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern uint GetGuiResources(IntPtr hProcess, uint flags);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int max);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int max);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr wp, IntPtr lp);
    public delegate bool EnumProc(IntPtr h, IntPtr p);
}
"@

$SW_MAXIMIZE = 3; $SW_MINIMIZE = 6; $SW_RESTORE = 9; $SW_SHOWNORMAL = 1
$WM_CLOSE = 0x0010
$GR_GDI = 0; $GR_USER = 1

# ----------------------------------------------------------------------------
# Paths / state
# ----------------------------------------------------------------------------
$exeDir     = Split-Path $ExePath
$auditPath  = Join-Path $exeDir "audit.log"
$iniPath    = Join-Path $exeDir "MBBSLauncher.ini"
$iniBackup  = Join-Path $exeDir "MBBSLauncher.ini.soakbackup"
$sandbox    = Join-Path $exeDir "_soak_sandbox"
$tickler    = Join-Path $sandbox "TICKLER.RUN"
$stamp      = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $exeDir "soak-report-$stamp.txt"

$rng      = [System.Random]::new($Seed)
$standins = @{}          # name -> System.Diagnostics.Process
$findings = New-Object System.Collections.Generic.List[string]
$samples  = New-Object System.Collections.Generic.List[object]
$auditLen = 0

function Log($msg) {
    $line = "[{0}] {1}" -f (Get-Date -Format "HH:mm:ss"), $msg
    Write-Host $line
    Add-Content -Path $reportPath -Value $line
}
function Finding($msg) {
    Log "  !! FINDING: $msg"
    $findings.Add($msg)
}

# ----------------------------------------------------------------------------
# audit.log delta scan for exceptions
# ----------------------------------------------------------------------------
function Get-AuditDelta {
    if (-not (Test-Path $auditPath)) { return @() }
    $all = Get-Content -Path $auditPath -ErrorAction SilentlyContinue
    if ($null -eq $all) { return @() }
    $new = if ($all.Count -gt $script:auditLen) { $all[$script:auditLen..($all.Count-1)] } else { @() }
    $script:auditLen = $all.Count
    return $new
}
function Scan-Audit($context) {
    foreach ($line in (Get-AuditDelta)) {
        if ($line -match "Exception:" -or $line -match "UnhandledException" -or `
            $line -match "ThreadException" -or $line -match "Stack Trace") {
            Finding "audit.log exception during '$context': $line"
        }
    }
}

# ----------------------------------------------------------------------------
# Stand-in BBS processes (copies of ping.exe, matched by name)
# ----------------------------------------------------------------------------
function Ensure-Standin($name) {
    $dest = Join-Path $sandbox "$name.exe"
    if (-not (Test-Path $dest)) { Copy-Item "$env:SystemRoot\System32\PING.EXE" $dest -Force }
    return $dest
}
function Start-Standin($name) {
    if ($standins.ContainsKey($name) -and -not $standins[$name].HasExited) { return }
    $exe = Ensure-Standin $name
    $p = Start-Process -FilePath $exe -ArgumentList "127.0.0.1","-t" -WindowStyle Hidden -PassThru
    $standins[$name] = $p
    Log "  standin UP:   $name (pid $($p.Id))"
}
function Stop-Standin($name) {
    if ($standins.ContainsKey($name)) {
        $p = $standins[$name]
        try { if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force } } catch {}
        $standins.Remove($name) | Out-Null
        Log "  standin DOWN: $name"
    }
}
function Stop-AllStandins { foreach ($n in @($standins.Keys)) { Stop-Standin $n } }

# ----------------------------------------------------------------------------
# Launcher control
# ----------------------------------------------------------------------------
function Get-LauncherWindow($proc) {
    $proc.Refresh()
    if ($proc.MainWindowHandle -ne [IntPtr]::Zero) { return $proc.MainWindowHandle }
    # Fallback: enumerate top-level windows owned by this pid
    $found = [IntPtr]::Zero
    $cb = [W32+EnumProc]{
        param($h,$p)
        $wpid = 0; [void][W32]::GetWindowThreadProcessId($h, [ref]$wpid)
        if ($wpid -eq $proc.Id -and [W32]::IsWindowVisible($h)) {
            $sb = New-Object System.Text.StringBuilder 256
            [void][W32]::GetClassName($h, $sb, 256)
            if ($sb.ToString() -notlike "#32770") { $script:found = $h; return $false }
        }
        return $true
    }
    [void][W32]::EnumWindows($cb, [IntPtr]::Zero)
    return $found
}

function Start-Launcher {
    # Refuse to collide with an existing instance
    $existing = Get-Process MBBSLauncher -ErrorAction SilentlyContinue
    if ($existing) {
        Finding "An MBBSLauncher instance was already running before a launch; killing it."
        $existing | Stop-Process -Force; Start-Sleep -Milliseconds 800
    }
    $p = Start-Process -FilePath $ExePath -PassThru
    $hwnd = [IntPtr]::Zero
    for ($i=0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 250
        if ($p.HasExited) { Finding "Launcher exited immediately after start (exit $($p.ExitCode))."; return $null }
        $hwnd = Get-LauncherWindow $p
        if ($hwnd -ne [IntPtr]::Zero) { break }
    }
    Start-Sleep -Milliseconds 500
    Scan-Audit "startup"
    Check-Dialogs "startup" $p
    if ($hwnd -eq [IntPtr]::Zero) { Log "  (no main window handle found; app may have started to tray)" }
    return $p
}

function Stop-Launcher($p) {
    if ($null -eq $p) { return }
    try {
        if (-not $p.HasExited) {
            $p.CloseMainWindow() | Out-Null
            if (-not $p.WaitForExit(4000)) { $p.Kill(); Finding "Launcher did not exit on CloseMainWindow; had to Kill." }
        }
    } catch {}
    Get-Process MBBSLauncher -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 400
}

# ----------------------------------------------------------------------------
# Dialog detection (stray modal dialogs owned by the launcher)
# ----------------------------------------------------------------------------
function Check-Dialogs($context, $proc) {
    $hits = @()
    $cb = [W32+EnumProc]{
        param($h,$p)
        $wpid = 0; [void][W32]::GetWindowThreadProcessId($h, [ref]$wpid)
        if ($wpid -eq $proc.Id -and [W32]::IsWindowVisible($h)) {
            $cls = New-Object System.Text.StringBuilder 256
            [void][W32]::GetClassName($h, $cls, 256)
            if ($cls.ToString() -eq "#32770") {   # standard Win32 dialog class
                $txt = New-Object System.Text.StringBuilder 512
                [void][W32]::GetWindowText($h, $txt, 512)
                $script:hits += ,@($h, $txt.ToString())
            }
        }
        return $true
    }
    [void][W32]::EnumWindows($cb, [IntPtr]::Zero)
    foreach ($d in $hits) {
        Finding "Modal dialog during '$context': '$($d[1])' — dismissing to continue."
        [void][W32]::PostMessage($d[0], $WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero)
        Start-Sleep -Milliseconds 300
    }
}

# ----------------------------------------------------------------------------
# Resource sampling (leak detection)
# ----------------------------------------------------------------------------
function Sample-Resources($proc, $label) {
    try {
        $proc.Refresh()
        if ($proc.HasExited) { return }
        $gdi  = [W32]::GetGuiResources($proc.Handle, $GR_GDI)
        $user = [W32]::GetGuiResources($proc.Handle, $GR_USER)
        $obj = [pscustomobject]@{
            Label=$label; Handles=$proc.HandleCount; GDI=$gdi; USER=$user
            WorkingSetMB=[math]::Round($proc.WorkingSet64/1MB,1)
        }
        $samples.Add($obj)
        Log ("  res[{0}] handles={1} gdi={2} user={3} ws={4}MB" -f $label,$obj.Handles,$gdi,$user,$obj.WorkingSetMB)
    } catch {}
}

# ----------------------------------------------------------------------------
# Window fuzz + BBS lifecycle simulation
# ----------------------------------------------------------------------------
$work = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea

function Fuzz-Window($proc) {
    $hwnd = Get-LauncherWindow $proc
    if ($hwnd -eq [IntPtr]::Zero) { return }
    switch ($rng.Next(0,6)) {
        0 { $x=$rng.Next($work.X, [Math]::Max($work.X+1,$work.Right-300)); $y=$rng.Next($work.Y, [Math]::Max($work.Y+1,$work.Bottom-200))
            $w=$rng.Next(640,960); $h=$rng.Next(360,540)
            [void][W32]::MoveWindow($hwnd,$x,$y,$w,$h,$true); Log "  win: move/resize $w x $h @ $x,$y" }
        1 { [void][W32]::ShowWindow($hwnd,$SW_MINIMIZE); Log "  win: minimize" }
        2 { [void][W32]::ShowWindow($hwnd,$SW_MAXIMIZE); Log "  win: maximize" }
        3 { [void][W32]::ShowWindow($hwnd,$SW_RESTORE); Log "  win: restore" }
        4 { [void][W32]::ShowWindow($hwnd,$SW_SHOWNORMAL); [void][W32]::SetForegroundWindow($hwnd); Log "  win: activate" }
        5 { if ($DriveKeys) {
                [void][W32]::SetForegroundWindow($hwnd); Start-Sleep -Milliseconds 200
                try { [System.Windows.Forms.SendKeys]::SendWait("{F12}"); Log "  keys: F12 (open config)"; Start-Sleep -Milliseconds 800
                      Check-Dialogs "after-F12" $proc
                      [System.Windows.Forms.SendKeys]::SendWait("{ESC}"); Log "  keys: ESC" } catch {}
            } else { [void][W32]::ShowWindow($hwnd,$SW_RESTORE) } }
    }
}

# BBS state model: 0=down, running=wgserver+wgsappgo up, cleanup=wgserver down/wgsappgo up,
# shutdown=both down + no tickler, crash=both down + tickler present
function Set-BbsState($state) {
    switch ($state) {
        "running"  { Start-Standin "wgserver"; Start-Standin "wgsappgo"; Remove-Item $tickler -ErrorAction SilentlyContinue; Set-Content $tickler "run" }
        "cleanup"  { Stop-Standin "wgserver"; Start-Standin "wgsappgo" }   # supervisor stays up
        "bounce"   { Stop-Standin "wgserver"; Stop-Standin "wgsappgo" }    # brief: both gone (debounce window)
        "shutdown" { Stop-Standin "wgserver"; Stop-Standin "wgsappgo"; Remove-Item $tickler -ErrorAction SilentlyContinue }
        "crash"    { Stop-Standin "wgserver"; Stop-Standin "wgsappgo"; Set-Content $tickler "run" }
    }
    Log "  bbs -> $state"
}

# ----------------------------------------------------------------------------
# Setup / teardown
# ----------------------------------------------------------------------------
function Setup {
    Log "=== MBBSLauncher soak harness — $stamp ==="
    Log "exe=$ExePath  seed=$Seed  cycles=$Cycles  relaunches=$Relaunches  driveKeys=$DriveKeys"

    if (-not (Test-Path $ExePath)) { throw "Launcher not found: $ExePath" }

    # Safety: refuse to run if the real BBS is up (we must never disturb it)
    $realProcs = Get-Process wgserver,wgsappgo -ErrorAction SilentlyContinue
    if ($realProcs) { throw "Real BBS processes are running (wgserver/wgsappgo). Stop the BBS before soak testing." }

    New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
    Ensure-Standin "wgserver" | Out-Null
    Ensure-Standin "wgsappgo" | Out-Null

    # Back up the real ini and drop in a safe sandbox test config
    if (Test-Path $iniPath) { Copy-Item $iniPath $iniBackup -Force; Log "backed up ini -> $iniBackup" }
    @"
; Soak-test config — generated by Soak-Launcher.ps1
[Paths]
BBSPath=$sandbox
[Window]
X=200
Y=150
Width=960
Height=540
[Settings]
AutoLaunchAtStartup=false
MinimizeToTray=true
ShowTrayIcon=true
EscMinimizesToTray=true
AutoStartBBS=false
AutoStartDelay=5
QuietMode=true
EasterEggsEnabled=true
AutoClickBtrieveContinue=false
[AppManager]
AutoShow=true
AutoHide=true
AlwaysOnTop=false
Opacity=60
[Monitoring]
Enabled=false
"@ | Set-Content -Path $iniPath -Encoding ASCII
    Log "wrote sandbox test ini (BBSPath=$sandbox, AutoStartBBS=off, Monitoring=off)"

    if (Test-Path $auditPath) { $script:auditLen = (Get-Content $auditPath).Count } else { $script:auditLen = 0 }
}

function Teardown {
    Log "--- teardown ---"
    Get-Process MBBSLauncher -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Stop-AllStandins
    Remove-Item $tickler -ErrorAction SilentlyContinue
    if (Test-Path $iniBackup) { Copy-Item $iniBackup $iniPath -Force; Remove-Item $iniBackup -Force; Log "restored original ini" }

    # Leak summary: compare first vs last resource sample per running instance
    if ($samples.Count -ge 2) {
        $first = $samples[0]; $last = $samples[$samples.Count-1]
        Log ("resource trend (first->last): handles {0}->{1}  gdi {2}->{3}  user {4}->{5}  ws {6}->{7}MB" -f `
            $first.Handles,$last.Handles,$first.GDI,$last.GDI,$first.USER,$last.USER,$first.WorkingSetMB,$last.WorkingSetMB)
        if (($last.GDI - $first.GDI) -gt 200)   { Finding "Possible GDI leak: GDI objects grew $($first.GDI) -> $($last.GDI)." }
        if (($last.USER - $first.USER) -gt 200) { Finding "Possible USER-object leak: $($first.USER) -> $($last.USER)." }
        if (($last.Handles - $first.Handles) -gt 400) { Finding "Possible handle leak: $($first.Handles) -> $($last.Handles)." }
    }

    Log "=== SUMMARY: $($findings.Count) finding(s) ==="
    if ($findings.Count -eq 0) { Log "No bugs detected in this run." }
    else { $i=1; foreach ($f in $findings) { Log ("  {0}. {1}" -f $i,$f); $i++ } }
    Log "Full report: $reportPath"
}

# ----------------------------------------------------------------------------
# Main
# ----------------------------------------------------------------------------
try {
    Setup

    # --- Phase A: single-instance soak (leak + state-machine + window fuzz) ---
    Log ""
    Log "### Phase A: soak ($Cycles cycles, one running instance) ###"
    $p = Start-Launcher
    if ($null -ne $p) {
        Set-BbsState "running"; Start-Sleep -Seconds 4      # let it see the BBS 'up' first
        Scan-Audit "bbs-running"
        Sample-Resources $p "cycle-0"
        $states = @("cleanup","running","bounce","running","shutdown","running","crash","running","cleanup","running")
        for ($c=1; $c -le $Cycles; $c++) {
            Log "-- cycle $c/$Cycles --"
            Fuzz-Window $p
            Start-Sleep -Milliseconds ($rng.Next(300,900))
            $st = $states[$rng.Next(0,$states.Length)]
            Set-BbsState $st
            # hold long enough for the 2s AppManager timer + ~6s debounce to act
            Start-Sleep -Seconds 8
            if ($p.HasExited) { Finding "Launcher exited unexpectedly during soak cycle $c (state=$st)."; break }
            Scan-Audit "cycle$c-$st"
            Check-Dialogs "cycle$c-$st" $p
            Sample-Resources $p "cycle-$c"
        }
        Set-BbsState "shutdown"
        Stop-Launcher $p
        Scan-Audit "after-stop"
    }

    # --- Phase B: open/close churn (startup + shutdown + single-instance) ---
    Log ""
    Log "### Phase B: open/close churn ($Relaunches rounds) ###"
    for ($r=1; $r -le $Relaunches; $r++) {
        Log "-- relaunch $r/$Relaunches --"
        Set-BbsState "shutdown"
        $p = Start-Launcher
        if ($null -eq $p) { continue }
        Set-BbsState "running"; Start-Sleep -Seconds 3
        Fuzz-Window $p; Start-Sleep -Milliseconds 500
        Fuzz-Window $p; Start-Sleep -Milliseconds 500
        Set-BbsState "cleanup"; Start-Sleep -Seconds 5
        Set-BbsState "shutdown"; Start-Sleep -Seconds 5
        if ($p.HasExited) { Finding "Launcher exited unexpectedly during relaunch round $r." }
        Scan-Audit "relaunch$r"
        Check-Dialogs "relaunch$r" $p
        Stop-Launcher $p

        # single-instance probe: try to start a 2nd copy, expect it to bow out
        $p2 = Start-Process -FilePath $ExePath -PassThru
        Start-Sleep -Seconds 2
        $live = Get-Process MBBSLauncher -ErrorAction SilentlyContinue
        if ($live -and $live.Count -gt 1) { Finding "Single-instance guard failed: $($live.Count) MBBSLauncher processes after 2nd launch." }
        Get-Process MBBSLauncher -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 400
    }
}
catch {
    Finding "HARNESS ERROR: $($_.Exception.Message)"
    Log $_.ScriptStackTrace
}
finally {
    Teardown
}
