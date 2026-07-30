# Measure-BBSDownWindow.ps1
# Samples wgserver.exe / wgsappgo.exe every 200ms and logs every transition, plus the
# duration of every window where BOTH are absent. Run it, then do a real CLEANUP and a
# real shutdown on the BBS. The "BOTH DOWN for N ms -> ended by <proc> reappearing"
# lines are the number the launcher's debounce has to survive.
#
# Usage:  powershell -ExecutionPolicy Bypass -File Measure-BBSDownWindow.ps1
#         Ctrl+C to stop. Log written beside this script as bbs-down-window.log.

$log = Join-Path $PSScriptRoot 'bbs-down-window.log'
$sw  = [System.Diagnostics.Stopwatch]::StartNew()

function Up([string]$n) { @(Get-Process -Name $n -ErrorAction SilentlyContinue).Count -gt 0 }
function Say([string]$m) {
    $line = '{0:HH:mm:ss.fff}  {1}' -f (Get-Date), $m
    Write-Host $line
    Add-Content -Path $log -Value $line
}

Say "=== sampler started (200ms) ==="

$prevSrv = Up 'wgserver'
$prevSup = Up 'wgsappgo'
Say ("initial: wgserver={0} wgsappgo={1}" -f $prevSrv, $prevSup)

$bothDownStart = $null

while ($true) {
    Start-Sleep -Milliseconds 200
    $srv = Up 'wgserver'
    $sup = Up 'wgsappgo'

    if ($srv -ne $prevSrv) { Say ("wgserver  -> {0}" -f $(if ($srv) {'UP'} else {'DOWN'})) }
    if ($sup -ne $prevSup) { Say ("wgsappgo  -> {0}" -f $(if ($sup) {'UP'} else {'DOWN'})) }

    $bothDown = (-not $srv) -and (-not $sup)

    if ($bothDown -and -not $bothDownStart) {
        $bothDownStart = $sw.Elapsed
        Say '*** BOTH DOWN - window opened'
    }
    elseif (-not $bothDown -and $bothDownStart) {
        $ms = [int]($sw.Elapsed - $bothDownStart).TotalMilliseconds
        $who = if ($srv -and $sup) { 'both' } elseif ($srv) { 'wgserver' } else { 'wgsappgo' }
        Say ("*** BOTH DOWN for {0} ms -> ended by {1} reappearing  <<< THIS IS THE BOUNCE THE DEBOUNCE MUST RIDE OUT" -f $ms, $who)
        $bothDownStart = $null
    }

    $prevSrv = $srv
    $prevSup = $sup
}

