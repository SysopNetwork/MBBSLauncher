// MBBSLauncher - wgserver Crash-Dialog Watcher
// Created by Mark Laudenbach with Love in Iowa
// https://github.com/SysopNetwork/MBBSLauncher
//
// File: Core/CrashDialogWatcher.cs
// Version: v2.0-beta21
//
// Change History:
// 26.07.15.1 - v2.0-beta20 - Initial creation. Lightweight always-on timer that periodically asks
//                     DialogCloser to clear a wgserver crash / "Application Error" box. When wgserver
//                     faults, Windows keeps the faulted process suspended-but-listed behind the modal
//                     box, so IsProcessRunning("wgserver") stays true and neither crash detector fires
//                     until a human closes the box. This watcher removes that obstruction so crash
//                     recovery (monitoring on) or launcher restore (monitoring off) proceeds without
//                     anyone present. Deliberately standalone of the BBS Monitoring engine so it works
//                     whether or not monitoring is enabled. Runs every second so recovery is prompt.
//                     Enabled via the [Settings] AutoDismissCrashDialog key (default on).
// 26.07.29.1 - v2.0-beta21 - Each tick also clears wgsappgo's "Unrecognized errorlevel ... returned
//                     from WGSERVER.EXE!" box (DialogCloser.DismissWgsappgoErrorlevelDialogIfPresent).
//                     That box keeps the supervisor alive after a hard wgserver death, which the
//                     beta15 cleanup-safety rule reads as "Cleanup..." — blocking both crash
//                     detectors until a human clicks OK.

using System;
using System.Windows.Forms;

namespace MBBSLauncher.Core
{
    /// <summary>
    /// Polls on a timer for a stuck wgserver crash box and clears it via
    /// <see cref="DialogCloser.DismissWgserverCrashDialogIfPresent"/>. The work is a cheap no-op
    /// whenever the BBS is healthy (no such box exists), so a fast poll is harmless.
    /// </summary>
    public class CrashDialogWatcher
    {
        private readonly Timer _timer;

        public CrashDialogWatcher(int intervalMs = 1000)
        {
            _timer = new Timer { Interval = intervalMs };
            _timer.Tick += Timer_Tick;
        }

        /// <summary>Begins watching for a wgserver crash box.</summary>
        public void Start() => _timer.Start();

        /// <summary>Stops watching.</summary>
        public void Stop() => _timer.Stop();

        private void Timer_Tick(object? sender, EventArgs e)
        {
            try
            {
                DialogCloser.DismissWgserverCrashDialogIfPresent();
                DialogCloser.DismissWgsappgoErrorlevelDialogIfPresent();
            }
            catch (Exception ex)
            {
                Program.LogError("CrashDialogWatcher", ex);
            }
        }
    }
}
