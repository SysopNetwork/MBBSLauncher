// MBBSLauncher - BBS Monitoring / Restart Manager
// Created by Mark Laudenbach with Love in Iowa
// https://github.com/SysopNetwork/MBBSLauncher
//
// File: Core/RestartManager.cs
// Version: v2.0-beta15
//
// The concept of monitoring The Major BBS for crashes and automatically restarting it was
// suggested by Steve Hartsock. Thank you, Steve.
//
// Change History:
// 26.02.07.1 - Initial creation (disabled stub in v1.5, kept for model/event compatibility)
// 26.06.22.1 - v2.0 - Implemented the BBS Monitoring engine. Self-contained 1-second state machine
//                     that watches for a crash (wgserver not running while the TICKLER module's
//                     TICKLER.RUN file is still present), then runs a two-stage recovery:
//                       Stage 1 - restart the BBS software, 1-3 attempts, each with a cancelable
//                                 countdown and a startup-grace window to confirm it came back up.
//                       Stage 2 - reboot the machine once (cancelable countdown), guarded against
//                                 reboot loops by a flag file that clears after a healthy period.
//                     UI-agnostic: raises StatusChanged / IncidentResolved events for the owner to
//                     render and to handle the restore-and-alert when everything is exhausted.
//                     Requires the TICKLER module: https://github.com/SysopNetwork/TICKLER
// 26.07.04.1 - v2.0-beta15 - Cleanup-safety fix: never restart the BBS while the wgsappgo supervisor
//                     is still alive. A crash now requires wgserver down AND wgsappgo gone AND
//                     TICKLER.RUN present; the Confirming and RestartCountdown phases also stand down
//                     immediately if the supervisor reappears. Prevents a nightly-cleanup timing gap
//                     (wgserver briefly down while wgsappgo runs maintenance) from being misread as a
//                     crash and double-opening the Btrieve databases (which forced a reindex).

using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using MBBSLauncher.Models;

namespace MBBSLauncher.Core
{
    /// <summary>Phases of a crash-recovery incident.</summary>
    public enum MonitorPhase
    {
        Idle,
        Confirming,        // crash suspected, debouncing before we act
        RestartCountdown,  // cancelable countdown before a restart attempt
        Launching,         // restart issued, waiting for the BBS to come back up
        AttemptDelay,      // waiting between attempts
        RebootCountdown,   // cancelable countdown before a machine reboot
        Rebooting          // reboot issued
    }

    /// <summary>Final outcome of a recovery incident.</summary>
    public enum MonitorOutcome
    {
        Recovered,   // BBS came back up (on its own or after a restart)
        Cancelled,   // sysop cancelled
        Rebooting,   // machine is rebooting
        Failed       // all enabled stages exhausted, BBS still down
    }

    public class MonitorStatusEventArgs : EventArgs
    {
        public MonitorPhase Phase { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool Cancelable { get; set; }
    }

    public class MonitorResultEventArgs : EventArgs
    {
        public MonitorOutcome Outcome { get; set; }
    }

    /// <summary>
    /// Monitors the BBS for a crash (via the TICKLER.RUN heartbeat) and drives the two-stage
    /// auto-restart recovery. Drive is a single 1-second WinForms timer on the UI thread.
    /// </summary>
    public class RestartManager
    {
        private const string WGSERVER_PROCESS = "wgserver";
        private const string WGSAPPGO_PROCESS = "wgsappgo";  // the "Go!" supervisor that owns wgserver

        private AutoRestartSettings _settings;
        private readonly ConfigManager _config;
        private readonly Timer _timer;

        private MonitorPhase _phase = MonitorPhase.Idle;
        private int _phaseSeconds;          // seconds elapsed in the current phase
        private int _attempt;               // current restart attempt (1-based)
        private DateTime? _healthySince;    // when the BBS was first seen healthy (for reboot-guard reset)
        private bool _suppressUntilHealthy; // after a cancel/failure, don't re-trigger until BBS is healthy again
        private bool _armed;                // only act on a BBS we've actually seen running this session

        /// <summary>Callback that restarts the BBS software. Returns true if a launch was initiated.</summary>
        public Func<bool>? LaunchBBS { get; set; }

        /// <summary>Raised each second while an incident is active, with text for the incident UI.</summary>
        public event EventHandler<MonitorStatusEventArgs>? StatusChanged;

        /// <summary>Raised once when an incident ends (recovered, cancelled, rebooting, or failed).</summary>
        public event EventHandler<MonitorResultEventArgs>? IncidentResolved;

        public bool IsHandlingIncident => _phase != MonitorPhase.Idle;

        public RestartManager(AutoRestartSettings settings, ConfigManager config)
        {
            _settings = settings;
            _config = config;
            _timer = new Timer { Interval = 1000 };
            _timer.Tick += Timer_Tick;
        }

        /// <summary>Begins monitoring (no-op until the BBS is running and then crashes).</summary>
        public void Start() => _timer.Start();

        /// <summary>Stops monitoring and clears any in-flight incident state.</summary>
        public void Stop()
        {
            _timer.Stop();
            _phase = MonitorPhase.Idle;
        }

        /// <summary>Re-reads settings after the sysop edits configuration.</summary>
        public void UpdateSettings(AutoRestartSettings settings) => _settings = settings;

        private string BbsPath => _config.GetValue("Paths", "BBSPath", "");
        private bool BbsUp() => ProcessHelper.IsProcessRunning(WGSERVER_PROCESS);
        private bool SupervisorUp() => ProcessHelper.IsProcessRunning(WGSAPPGO_PROCESS);
        private bool TicklerPresent() => TicklerMonitor.Exists(BbsPath);

        // A genuine crash means the WHOLE BBS is down. During a nightly cleanup the wgserver process
        // exits and restarts while the wgsappgo supervisor stays alive — and if wgsappgo is alive it
        // will relaunch wgserver on its own anyway. In either case the launcher must NOT jump in and
        // start a second copy: doing so double-opens the Btrieve databases and forces a reindex.
        // So a crash requires wgserver down AND the supervisor gone AND the TICKLER.RUN heartbeat still
        // present (a clean shutdown/cleanup deletes it). The supervisor check is the hard guard that
        // keeps us out even if TICKLER.RUN lingers due to a cleanup timing gap.
        private bool CrashCondition() => !BbsUp() && !SupervisorUp() && TicklerPresent();

        private void Timer_Tick(object? sender, EventArgs e)
        {
            // Disabled mid-incident (sysop turned monitoring off) — abort cleanly.
            if (!_settings.Enabled)
            {
                if (_phase != MonitorPhase.Idle) EndIncident(MonitorOutcome.Cancelled, "Monitoring disabled — recovery aborted.");
                return;
            }

            switch (_phase)
            {
                case MonitorPhase.Idle:            TickIdle();            break;
                case MonitorPhase.Confirming:      TickConfirming();      break;
                case MonitorPhase.RestartCountdown:TickRestartCountdown();break;
                case MonitorPhase.Launching:       TickLaunching();       break;
                case MonitorPhase.AttemptDelay:    TickAttemptDelay();    break;
                case MonitorPhase.RebootCountdown: TickRebootCountdown(); break;
                case MonitorPhase.Rebooting:       /* waiting for OS */   break;
            }
        }

        private void TickIdle()
        {
            if (BbsUp())
            {
                _armed = true;                 // we've seen the BBS up — crashes are now actionable
                _suppressUntilHealthy = false; // BBS healthy again — re-arm
                TrackHealthy();
                return;
            }

            _healthySince = null;

            if (!_armed) return;                        // never seen the BBS up yet — ignore stale state at startup
            if (_suppressUntilHealthy) return;          // sysop cancelled/failed; wait for a healthy BBS first
            if (!CrashCondition()) return;              // BBS down but TICKLER gone = clean shutdown/cleanup

            // Crash suspected — begin debounce.
            _phase = MonitorPhase.Confirming;
            _phaseSeconds = 0;
            Program.LogInfo("Monitoring", "Crash suspected (wgserver down, TICKLER.RUN present) — confirming.");
            RaiseStatus("BBS crash suspected — confirming...", cancelable: true);
        }

        private void TickConfirming()
        {
            if (BbsUp()) { EndIncident(MonitorOutcome.Recovered, "BBS recovered on its own."); return; }
            if (SupervisorUp()) { EndIncident(MonitorOutcome.Recovered, "wgsappgo supervisor still active — cleanup in progress, not a crash."); return; }
            if (!TicklerPresent()) { EndIncident(MonitorOutcome.Recovered, "TICKLER.RUN cleared — clean shutdown, not a crash."); return; }

            _phaseSeconds++;
            if (_phaseSeconds >= _settings.CrashConfirmSeconds)
            {
                _attempt = 1;
                EnterRestartCountdown();
            }
            else
            {
                RaiseStatus($"BBS crash suspected — confirming ({_settings.CrashConfirmSeconds - _phaseSeconds}s)...", cancelable: true);
            }
        }

        private void EnterRestartCountdown()
        {
            _phase = MonitorPhase.RestartCountdown;
            _phaseSeconds = 0;
            RaiseRestartCountdown();
        }

        private void TickRestartCountdown()
        {
            if (BbsUp()) { EndIncident(MonitorOutcome.Recovered, "BBS recovered before restart."); return; }
            if (SupervisorUp()) { EndIncident(MonitorOutcome.Recovered, "wgsappgo supervisor reappeared — cleanup/self-recovery in progress, standing down."); return; }

            int remaining = _settings.RestartCountdownSeconds - _phaseSeconds;
            if (remaining <= 0)
            {
                DoLaunch();
                return;
            }

            RaiseRestartCountdown();
            _phaseSeconds++;
        }

        private void RaiseRestartCountdown()
        {
            int remaining = Math.Max(0, _settings.RestartCountdownSeconds - _phaseSeconds);
            RaiseStatus($"BBS crash detected. Restarting the BBS (attempt {_attempt} of {_settings.RestartAttempts}) in {Mmss(remaining)}...",
                cancelable: true);
        }

        private void DoLaunch()
        {
            Program.LogInfo("Monitoring", $"Restart attempt {_attempt} of {_settings.RestartAttempts} — launching the BBS.");
            RaiseStatus($"Restarting the BBS (attempt {_attempt} of {_settings.RestartAttempts})...", cancelable: false);

            bool launched = false;
            try { launched = LaunchBBS?.Invoke() ?? false; }
            catch (Exception ex) { Program.LogError("Monitoring.LaunchBBS", ex); }

            if (!launched)
                Program.LogInfo("Monitoring", $"Restart attempt {_attempt}: launch could not be initiated.");

            _phase = MonitorPhase.Launching;
            _phaseSeconds = 0;
        }

        private void TickLaunching()
        {
            if (BbsUp())
            {
                Program.LogInfo("Monitoring", $"BBS came back up after restart attempt {_attempt}.");
                EndIncident(MonitorOutcome.Recovered, "BBS restarted successfully.");
                return;
            }

            _phaseSeconds++;
            int remaining = _settings.StartupGraceSeconds - _phaseSeconds;
            if (remaining <= 0)
            {
                Program.LogInfo("Monitoring", $"Restart attempt {_attempt} failed (BBS did not come up within {_settings.StartupGraceSeconds}s).");
                if (_attempt < _settings.RestartAttempts)
                {
                    _attempt++;
                    _phase = MonitorPhase.AttemptDelay;
                    _phaseSeconds = 0;
                }
                else
                {
                    EnterStage2OrFail();
                }
            }
            else
            {
                RaiseStatus($"Restarting the BBS (attempt {_attempt} of {_settings.RestartAttempts}) — waiting for it to come up ({Mmss(remaining)})...",
                    cancelable: false);
            }
        }

        private void TickAttemptDelay()
        {
            if (BbsUp()) { EndIncident(MonitorOutcome.Recovered, "BBS recovered between attempts."); return; }

            int remaining = _settings.AttemptDelaySeconds - _phaseSeconds;
            if (remaining <= 0)
            {
                EnterRestartCountdown();
            }
            else
            {
                RaiseStatus($"Restart failed. Next attempt ({_attempt} of {_settings.RestartAttempts}) in {Mmss(remaining)}...", cancelable: true);
                _phaseSeconds++;
            }
        }

        private void EnterStage2OrFail()
        {
            if (_settings.RebootEnabled && !RebootGuardTripped())
            {
                _phase = MonitorPhase.RebootCountdown;
                _phaseSeconds = 0;
                Program.LogInfo("Monitoring", "Stage 1 exhausted — starting reboot countdown (Stage 2).");
                RaiseRebootCountdown();
            }
            else
            {
                string why = _settings.RebootEnabled
                    ? "reboot already performed once for this incident (loop guard)"
                    : "reboot stage disabled";
                Program.LogInfo("Monitoring", $"Stage 1 exhausted and no reboot ({why}). Restoring launcher and alerting the sysop.");
                EndIncident(MonitorOutcome.Failed, "BBS could not be restarted.");
            }
        }

        private void TickRebootCountdown()
        {
            if (BbsUp()) { EndIncident(MonitorOutcome.Recovered, "BBS recovered before reboot."); return; }

            int remaining = _settings.RebootCountdownSeconds - _phaseSeconds;
            if (remaining <= 0)
            {
                DoReboot();
                return;
            }

            RaiseRebootCountdown();
            _phaseSeconds++;
        }

        private void RaiseRebootCountdown()
        {
            int remaining = Math.Max(0, _settings.RebootCountdownSeconds - _phaseSeconds);
            RaiseStatus($"BBS could not be restarted. REBOOTING this computer in {Mmss(remaining)}... (Cancel to stop)", cancelable: true);
        }

        private void DoReboot()
        {
            _phase = MonitorPhase.Rebooting;
            Program.LogInfo("Monitoring", "Rebooting the machine now (Stage 2).");
            WriteRebootFlag();
            RaiseStatus("Rebooting this computer now...", cancelable: false);
            IncidentResolved?.Invoke(this, new MonitorResultEventArgs { Outcome = MonitorOutcome.Rebooting });

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "shutdown",
                    Arguments = "/r /t 5 /c \"MBBSLauncher: BBS could not be restarted — rebooting.\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            catch (Exception ex)
            {
                Program.LogError("Monitoring.Reboot", ex);
            }
        }

        /// <summary>Sysop cancelled the current countdown from the incident UI.</summary>
        public void CancelIncident()
        {
            if (_phase == MonitorPhase.Idle || _phase == MonitorPhase.Rebooting) return;
            Program.LogInfo("Monitoring", "Recovery cancelled by the sysop.");
            _suppressUntilHealthy = true; // don't immediately re-trigger; wait until the BBS is healthy again
            EndIncident(MonitorOutcome.Cancelled, "Recovery cancelled.");
        }

        private void EndIncident(MonitorOutcome outcome, string logMessage)
        {
            Program.LogInfo("Monitoring", logMessage);
            if (outcome == MonitorOutcome.Failed)
                _suppressUntilHealthy = true; // BBS stays down; wait for a healthy BBS before arming again

            _phase = MonitorPhase.Idle;
            _phaseSeconds = 0;
            _attempt = 0;
            IncidentResolved?.Invoke(this, new MonitorResultEventArgs { Outcome = outcome });
        }

        private void TrackHealthy()
        {
            if (_healthySince == null) _healthySince = DateTime.Now;

            // Clear the one-shot reboot guard once the BBS has been healthy for the reset window.
            if (RebootFlagExists() && (DateTime.Now - _healthySince.Value).TotalMinutes >= _settings.HealthyResetMinutes)
            {
                DeleteRebootFlag();
                Program.LogInfo("Monitoring", $"BBS healthy for {_settings.HealthyResetMinutes} min — reboot guard cleared.");
            }
        }

        private void RaiseStatus(string message, bool cancelable)
        {
            StatusChanged?.Invoke(this, new MonitorStatusEventArgs
            {
                Phase = _phase,
                Message = message,
                Cancelable = cancelable
            });
        }

        private static string Mmss(int totalSeconds)
        {
            if (totalSeconds < 0) totalSeconds = 0;
            return $"{totalSeconds / 60}:{totalSeconds % 60:D2}";
        }

        // --- Reboot-loop guard (flag file beside the exe) ---

        private static string RebootFlagPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "monitor-reboot.flag");

        private static bool RebootFlagExists()
        {
            try { return File.Exists(RebootFlagPath); } catch { return false; }
        }

        /// <summary>True if we already auto-rebooted and the BBS has not yet run healthy long enough to reset.</summary>
        private bool RebootGuardTripped() => RebootFlagExists();

        private void WriteRebootFlag()
        {
            try { File.WriteAllText(RebootFlagPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")); }
            catch (Exception ex) { Program.LogError("Monitoring.WriteRebootFlag", ex); }
        }

        private void DeleteRebootFlag()
        {
            try { if (File.Exists(RebootFlagPath)) File.Delete(RebootFlagPath); }
            catch (Exception ex) { Program.LogError("Monitoring.DeleteRebootFlag", ex); }
        }
    }
}
