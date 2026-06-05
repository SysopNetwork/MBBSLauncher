// MBBSLauncher - Program Entry Point
// Created by Mark Laudenbach with Love in Iowa
// https://github.com/SysopNetwork/MBBSLauncher
//
// File: Program.cs
// Version: v1.85
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

using System;
using System.IO;
using System.Windows.Forms;
using MBBSLauncher.Core;
using MBBSLauncher.Forms;

namespace MBBSLauncher
{
    internal static class Program
    {
        public const string APP_VERSION = "v1.85";
        public const string APP_NAME = "MBBSLauncher";
        public const string AUTHOR = "Mark Laudenbach";
        public const string TAGLINE = "Created with Love in Iowa, USA.";
        public const string GITHUB_URL = "https://github.com/SysopNetwork/MBBSLauncher";
        public const string DISCORD_URL = "https://discord.gg/dpzquuNVmb";

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

        public static void LogError(string context, Exception ex)
        {
            try
            {
                string logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audit.log");

                // Rotate log if it gets too large (> 500 KB)
                RotateLogIfNeeded(logFile, 500 * 1024);

                string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}\n" +
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
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Log rotated (previous log saved as audit.log.old)\n" +
                        $"----------------------------------------\n\n");
                }
            }
            catch
            {
                // If rotation fails, continue anyway
            }
        }
    }
}
