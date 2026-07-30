// MBBSLauncher - Btrieve Dialog Watcher
// Created by Mark Laudenbach with Love in Iowa
// https://github.com/SysopNetwork/MBBSLauncher
//
// File: Core/BtrieveDialogWatcher.cs
// Version: v2.0
//
// Change History:
// 26.06.22.1 - v2.0 - Initial creation. Lightweight always-on timer that periodically asks
//                     DialogCloser to click Yes on wgsappgo's Btrieve "already running" prompt so a
//                     post-crash BBS restart can finish without a human present. Deliberately
//                     standalone of the BBS Monitoring engine (RestartManager) so it also covers
//                     Worldgroup's own wgsysdn restarts and manual relaunches. Enabled via the
//                     [Settings] AutoClickBtrieveContinue key (default on).

using System;
using System.Windows.Forms;

namespace MBBSLauncher.Core
{
    /// <summary>
    /// Polls on a timer for the wgsappgo Btrieve "already running" prompt and auto-clicks Yes via
    /// <see cref="DialogCloser.ClickBtrieveContinueIfPresent"/>. The prompt waits for input
    /// indefinitely, so a slow poll is plenty; the work is also a cheap no-op whenever the BBS is up.
    /// </summary>
    public class BtrieveDialogWatcher
    {
        private readonly Timer _timer;

        public BtrieveDialogWatcher(int intervalMs = 2000)
        {
            _timer = new Timer { Interval = intervalMs };
            _timer.Tick += Timer_Tick;
        }

        /// <summary>Begins watching for the Btrieve prompt.</summary>
        public void Start() => _timer.Start();

        /// <summary>Stops watching.</summary>
        public void Stop() => _timer.Stop();

        private void Timer_Tick(object? sender, EventArgs e)
        {
            try
            {
                DialogCloser.ClickBtrieveContinueIfPresent();
            }
            catch (Exception ex)
            {
                Program.LogError("BtrieveWatcher", ex);
            }
        }
    }
}
