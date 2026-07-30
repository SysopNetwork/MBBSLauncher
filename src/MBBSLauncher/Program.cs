// MBBSLauncher - Program Entry Point
// Created by Mark Laudenbach with Love in Iowa
// https://github.com/SysopNetwork/MBBSLauncher
//
// File: Program.cs
// Version: v2.0-beta22
//
// Change History:
// 26.01.07.1 - 06:00PM - Initial creation
// 26.01.07.3 - 07:15PM - Added global exception handling and error logging
// 26.01.12.1 - Bumped version to v1.10
// 26.01.23.1 - Bumped version to v1.20 - Added Ghost3 support
// 26.02.07.1 - Bumped version to v1.5 - Self-contained deployment, multi-program auto-launch, 5-tab config
// 26.02.11.1 - Bumped version to v1.6 - Added administrator privileges requirement
// 26.02.18.1 - Bumped version to v1.55 - Auto Launch skips already-running processes
// 26.02.19.1 - Bumped version to v1.60 - UI improvements, wording corrections, layout fixes
// 26.02.19.2 - Bumped version to v1.70 - Bug fixes + App Manager opacity slider
// 26.06.04.1 - Bumped version to v1.80 - Cleanup delay, double-restore fix, countdown overlap fix, code fixes
// 26.06.04.2 - Bumped version to v1.85 - Bug fixes from v1.80 code review
// 26.06.22.1 - Bumped version to v1.90 - Auto-start re-trigger fix, paint-crash guard, bring
//                      launcher to foreground after programs close, "3rd Party App Launcher" rename
// 26.06.22.2 - v2.0-beta1 - Added BBS Monitoring & Auto-Restart (TICKLER.RUN crash detection,
//                      two-stage recovery). Added Program.LogInfo for the monitoring audit trail.
// 26.06.29.1 - v2.0-beta2 - TICKLER file check before enabling BBS Monitoring; exit warning
//                      when monitoring is active.
// 26.06.30.1 - v2.0-beta3 - BBS Monitoring now checks for SNTICKLR.DLL / SNTICKLR.MDF.
// 26.06.30.2 - v2.0-beta4 - Startup validation: if BBS Monitoring is enabled but SNTICKLR files
//                      are missing, auto-disable and alert the sysop.
// 26.06.30.3 - v2.0-beta6 - Enabling BBS Monitoring auto-installs SNTICKLR from the MBBS Module
//                      folder; disabling removes it from the BBS folder. Source files never deleted.
// 26.06.30.4 - v2.0-beta7 - All Stage 1 / Stage 2 / Timing settings are disabled when BBS
//                      Monitoring is unchecked; grayed out controls can't be configured out of context.
// 26.06.30.5 - v2.0-beta8 - Enabling or disabling BBS Monitoring is now blocked while the BBS is
//                      running — SNTICKLR files may be in use and cannot be installed or removed.
// 26.06.30.6 - v2.0-beta9 - SNTICKLR.DLL / SNTICKLR.MDF embedded in the exe (no more MBBS Module\
//                      folder in the release). Version-aware update: launcher checks installed DLL
//                      version against ModuleShipVersion on every startup and silently updates if
//                      the installed version is older. Never downgrades a newer installed version.
// 26.06.30.7 - v2.0-beta10 - App Manager always-on-top and auto-hide checkboxes now correctly
//                      reflect saved values on startup. Window position is now saved whenever
//                      the App Manager is hidden via its close button.
// 26.07.03.1 - v2.0-beta11 - New Config Editor option (General tab) to enable/disable the
//                      automatic App Manager popup when the BBS starts ([AppManager] AutoShow).
// 26.07.03.2 - v2.0-beta12 - New "Check for Updates" button (Config Editor -> Advanced tab). Manual
//                      only: queries the GitHub Releases API on click and reports whether a newer
//                      version exists, with the download page URL. The launcher never contacts
//                      GitHub on its own — no startup or background check. See Core/UpdateChecker.cs.
// 26.07.03.3 - v2.0-beta13 - Hidden features on the main screen: the Konami code shows a scrolling
//                      "greetz" credits screen, and typing C-G-A toggles a CGA color-cycle + CRT
//                      scanline overlay. Both are off until triggered and can be disabled via
//                      Config Editor -> Advanced -> Extras ([Settings] EasterEggsEnabled). See
//                      MainForm.EasterEggs.cs.
// 26.07.03.4 - v2.0-beta14 - Slowed the CGA overlay to a gentle drift and added a periodic split-
//                      second "teaser" blip (every 1-2 minutes, launcher visible only) to hint at
//                      the hidden features. Governed by the same EasterEggsEnabled toggle.
// 26.07.04.1 - v2.0-beta15 - BBS Monitoring cleanup-safety fix: the crash monitor no longer restarts
//                      the BBS while the wgsappgo supervisor is still alive (a nightly cleanup cycles
//                      wgserver while wgsappgo keeps running). Prevents a double-launch that could
//                      double-open the Btrieve databases and force a reindex. See Core/RestartManager.cs.
// 26.07.05.1 - v2.0-beta16 - Companion cleanup-safety fix in the App Manager's BBS-status detector
//                      (Forms/AppManagerForm.cs): it now checks the wgsappgo supervisor before TICKLER.RUN,
//                      so the start-of-cleanup window (wgserver already gone, TICKLER.RUN not yet deleted)
//                      is no longer misread as a crash that pops the launcher up when monitoring is off.
// 26.07.05.2 - v2.0-beta17 - Sysop still saw a mid-cleanup launcher pop-up with monitoring OFF and no
//                      TICKLER.RUN present. Hardened the App Manager detector (Forms/AppManagerForm.cs):
//                      the "both wgserver and wgsappgo gone" decision is now debounced over several
//                      consecutive checks before the launcher is restored (a cleanup that briefly bounces
//                      the Go! supervisor no longer reads as a shutdown), and every restore now logs the
//                      exact process/TICKLER.RUN state to audit.log so a misconfigured BBS path is visible.
// 26.07.05.3 - v2.0-beta18 - Audit-log build visibility: every audit.log line is now tagged with a
//                      compact build id (e.g. "[v2b18]") so a sysop can tell which version produced any
//                      given event, and a "Startup" banner records the full version plus the initial
//                      BBS/process state (monitoring on/off, wgserver/wgsappgo, TICKLER.RUN) at launch.
// 26.07.08.1 - v2.0-beta19 - Fix the launcher popping up on top of a program opened after a BBS
//                      shutdown. The BBS-stop restore is delayed (~6s App Manager debounce); if the
//                      sysop pops the launcher and opens a CNF/Module/Offline program in that window,
//                      the delayed restore no longer slams the launcher over it — it holds back when a
//                      launcher-managed child is still running. See Forms/MainForm.cs.
// 26.07.15.1 - v2.0-beta20 - Updated the Sysop Network Discord invite to https://discord.gg/ByM7Dm6bXY
//                      (DISCORD_URL constant, used by the Config Editor Support tab and F1 Help).
// 26.07.29.1 - v2.0-beta21 - Version bump. Crash-dialog watcher now also clears wgsappgo's
//                      "Unrecognized errorlevel ... returned from WGSERVER.EXE!" box, which kept the
//                      supervisor alive after a hard wgserver death and blocked both crash detectors
//                      (they read the alive supervisor as a cleanup). See Core/DialogCloser.cs.

using System;
using System.IO;
using System.Windows.Forms;
using MBBSLauncher.Core;
using MBBSLauncher.Forms;

namespace MBBSLauncher
{
    internal static class Program
    {
        public const string APP_VERSION = "v2.0-beta22";

        /// <summary>
        /// Compact form of APP_VERSION used as a per-line tag in audit.log so a glance at any event
        /// shows which build produced it. Collapses a ".0" minor and shortens the pre-release word:
        /// "v2.0-beta18" -> "v2b18", "v2.1-beta4" -> "v2.1b4", "v2.0" -> "v2", "v1.85" -> "v1.85".
        /// </summary>
        public static readonly string APP_VERSION_SHORT = ToShortVersion(APP_VERSION);

        public const string APP_NAME = "MBBSLauncher";
        public const string AUTHOR = "Mark Laudenbach";
        public const string TAGLINE = "Created with Love in Iowa, USA.";
        public const string GITHUB_URL = "https://github.com/SysopNetwork/MBBSLauncher";
        public const string DISCORD_URL = "https://discord.gg/ByM7Dm6bXY";

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Check for single instance
            if (!SingleInstanceManager.AcquireInstance())
            {
                // Another instance is already running - try to restore it
                if (SingleInstanceManager.RestoreExistingInstance())
                {
                    // Successfully restored existing instance
                    MessageBox.Show(
                        "MBBSLauncher is already running.\n\nThe existing window has been restored.",
                        "Already Running",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    // Could not restore, but another instance exists
                    MessageBox.Show(
                        "MBBSLauncher is already running.\n\nCould not restore the existing window - check your system tray.",
                        "Already Running",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                // Exit this instance
                return;
            }

            try
            {
                // Add global exception handlers
                Application.ThreadException += Application_ThreadException;
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

                // Check for v1.20 config migration
                if (ConfigMigration.NeedsMigration())
                {
                    var dialogResult = MessageBox.Show(
                        "Configuration Upgrade Required\n\n" +
                        "MBBSLauncher v1.20 configuration detected.\n\n" +
                        $"Your settings will be upgraded to {APP_VERSION} format.\n" +
                        "• All existing settings will be preserved\n" +
                        "• A backup will be created\n" +
                        "• New features will use default values\n\n" +
                        "Backup: MBBSLauncher.ini.v120.backup\n\n" +
                        "Continue with upgrade?",
                        "Configuration Upgrade",
                        MessageBoxButtons.OKCancel,
                        MessageBoxIcon.Information);

                    if (dialogResult == DialogResult.OK)
                    {
                        var result = ConfigMigration.MigrateV120ToV20();

                        if (result.Success)
                        {
                            MessageBox.Show(
                                "Configuration Upgraded Successfully!\n\n" +
                                $"Your settings have been upgraded to {APP_VERSION}.\n\n" +
                                $"✓ {result.MigratedSettings.Count} settings preserved\n" +
                                $"✓ Backup created\n" +
                                $"✓ New features available\n\n" +
                                $"Backup location:\n{result.BackupPath}",
                                "Upgrade Complete",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show(
                                "Configuration migration failed:\n\n" +
                                $"{result.ErrorMessage}\n\n" +
                                "Your original settings are safe.\n" +
                                "Please report this issue on GitHub.",
                                "Migration Failed",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);

                            // Exit - don't run with mixed config
                            return;
                        }
                    }
                    else
                    {
                        // User cancelled migration
                        MessageBox.Show(
                            "Configuration upgrade cancelled.\n\n" +
                            "Please use MBBSLauncher v1.20 with this configuration.",
                            "Upgrade Cancelled",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }
                }
                else
                {
                    // No migration needed — silently ensure the new-style version marker is present.
                    // This upgrades existing installs that had the old [AutoLaunch].Version marker.
                    ConfigMigration.EnsureVersionMarker();
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                LogError("Main", ex);
                MessageBox.Show(
                    $"A fatal error occurred:\n\n{ex.Message}\n\n{ex.StackTrace}\n\nCheck audit.log for details.",
                    "MBBSLauncher - Fatal Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                // Release single instance mutex
                SingleInstanceManager.Release();
            }
        }

        private static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            LogError("ThreadException", e.Exception);
            MessageBox.Show(
                $"An error occurred:\n\n{e.Exception.Message}\n\nCheck audit.log for details.",
                "MBBSLauncher - Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogError("UnhandledException", ex);
                MessageBox.Show(
                    $"An unhandled error occurred:\n\n{ex.Message}\n\nCheck audit.log for details.",
                    "MBBSLauncher - Unhandled Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Writes a timestamped informational line to audit.log (used by BBS Monitoring to record
        /// crash detection, restart attempts, and reboots). Shares the same file and rotation as errors.
        /// </summary>
        public static void LogInfo(string context, string message)
        {
            try
            {
                string logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audit.log");
                RotateLogIfNeeded(logFile, 500 * 1024);
                File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{APP_VERSION_SHORT}] {context}: {message}\n");
            }
            catch
            {
                // If we can't log, at least we tried
            }
        }

        public static void LogError(string context, Exception ex)
        {
            try
            {
                string logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audit.log");

                // Rotate log if it gets too large (> 500 KB)
                RotateLogIfNeeded(logFile, 500 * 1024);

                string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{APP_VERSION_SHORT}] {context}\n" +
                                  $"Exception: {ex.GetType().Name}\n" +
                                  $"Message: {ex.Message}\n" +
                                  $"Stack Trace:\n{ex.StackTrace}\n" +
                                  $"----------------------------------------\n\n";
                File.AppendAllText(logFile, logMessage);
            }
            catch
            {
                // If we can't log, at least we tried
            }
        }

        private static void RotateLogIfNeeded(string logFile, long maxSizeBytes)
        {
            try
            {
                if (!File.Exists(logFile))
                    return;

                var fileInfo = new FileInfo(logFile);
                if (fileInfo.Length > maxSizeBytes)
                {
                    // Keep old log as .old, delete previous .old
                    string oldLog = logFile + ".old";
                    if (File.Exists(oldLog))
                        File.Delete(oldLog);

                    File.Move(logFile, oldLog);

                    // Start fresh log with rotation notice
                    File.WriteAllText(logFile,
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{APP_VERSION_SHORT}] Log rotated (previous log saved as audit.log.old)\n" +
                        $"----------------------------------------\n\n");
                }
            }
            catch
            {
                // If rotation fails, continue anyway
            }
        }

        /// <summary>
        /// Collapses a full version string to the short tag used on every audit.log line.
        /// "v2.0-beta18" -> "v2b18"; "v2.1-beta4" -> "v2.1b4"; "v2.0" -> "v2"; "v1.85" -> "v1.85".
        /// A ".0" minor is dropped; meaningful minors are kept. Never throws — a format quirk must
        /// not break logging, so on any surprise it returns the version unchanged.
        /// </summary>
        private static string ToShortVersion(string version)
        {
            try
            {
                string v = version.Trim();
                bool hadV = v.StartsWith("v", StringComparison.OrdinalIgnoreCase);
                if (hadV) v = v.Substring(1);

                string basePart = v;
                string suffix = string.Empty;
                int dash = v.IndexOf('-');
                if (dash >= 0)
                {
                    basePart = v.Substring(0, dash);
                    suffix = v.Substring(dash + 1);
                }

                // Collapse a ".0" minor (2.0 -> 2) but keep meaningful ones (2.1, 1.85).
                string[] parts = basePart.Split('.');
                if (parts.Length == 2 && parts[1] == "0")
                    basePart = parts[0];

                // Shorten the pre-release word: beta -> b, alpha -> a (rc left as-is).
                suffix = suffix
                    .Replace("beta", "b", StringComparison.OrdinalIgnoreCase)
                    .Replace("alpha", "a", StringComparison.OrdinalIgnoreCase);

                return (hadV ? "v" : string.Empty) + basePart + suffix;
            }
            catch
            {
                return version;
            }
        }
    }
}
