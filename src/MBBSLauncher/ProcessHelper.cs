// MBBSLauncher - Process Management Helper
// Created by Mark Laudenbach with Love in Iowa
// https://github.com/SysopNetwork/MBBSLauncher
//
// File: ProcessHelper.cs
// Version: v1.00
//
// Change History:
// 26.01.07.1 - 06:00PM - Initial creation

using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace MBBSLauncher
{
    /// <summary>
    /// Helper class for process management and window manipulation
    /// </summary>
    public static class ProcessHelper
    {
        #region Win32 API Imports

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;

        #endregion

        /// <summary>
        /// Checks if a process with the given name is currently running
        /// </summary>
        /// <param name="processName">Name of the process without .exe extension</param>
        /// <returns>True if process is running, false otherwise</returns>
        public static bool IsProcessRunning(string processName)
        {
            // Remove .exe extension if present
            if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                processName = processName.Substring(0, processName.Length - 4);

            Process[] processes = Process.GetProcessesByName(processName);
            return processes.Length > 0;
        }

        /// <summary>
        /// Gets the first process with the given name
        /// </summary>
        /// <param name="processName">Name of the process without .exe extension</param>
        /// <returns>Process object or null if not found</returns>
        public static Process? GetProcess(string processName)
        {
            // Remove .exe extension if present
            if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                processName = processName.Substring(0, processName.Length - 4);

            Process[] processes = Process.GetProcessesByName(processName);
            return processes.FirstOrDefault();
        }

        /// <summary>
        /// Brings a window to the foreground and restores it if minimized
        /// </summary>
        /// <param name="process">The process whose window to bring to foreground</param>
        /// <returns>True if successful, false otherwise</returns>
        public static bool BringToForeground(Process process)
        {
            try
            {
                IntPtr handle = process.MainWindowHandle;
                if (handle == IntPtr.Zero)
                    return false;

                // If window is minimized, restore it
                if (IsIconic(handle))
                {
                    ShowWindow(handle, SW_RESTORE);
                }
                else
                {
                    ShowWindow(handle, SW_SHOW);
                }

                // Bring window to foreground
                return SetForegroundWindow(handle);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Launches a program and waits for it to start
        /// </summary>
        /// <param name="programPath">Full path to the executable</param>
        /// <param name="workingDirectory">Working directory for the process</param>
        /// <param name="arguments">Command-line arguments for the program</param>
        /// <param name="minimized">Whether to launch minimized (doesn't steal focus)</param>
        /// <returns>The launched Process object or null if failed</returns>
        public static Process? LaunchProgram(string programPath, string? workingDirectory = null, string? arguments = null, bool minimized = false)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = programPath,
                    UseShellExecute = true
                };

                if (!string.IsNullOrEmpty(arguments))
                    startInfo.Arguments = arguments;

                if (!string.IsNullOrEmpty(workingDirectory))
                    startInfo.WorkingDirectory = workingDirectory;

                // Launch minimized if requested (useful for auto-launch programs)
                if (minimized)
                {
                    startInfo.WindowStyle = ProcessWindowStyle.Minimized;
                }

                return Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(
                    $"Error launching program:\n{programPath} {arguments}\n\n{ex.Message}",
                    "Launch Error",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
                return null;
            }
        }

        /// <summary>
        /// Checks if wgserver.exe is running (prevents launcher from opening during BBS operation)
        /// </summary>
        /// <returns>True if wgserver is running, false otherwise</returns>
        public static bool IsWGServerRunning()
        {
            return IsProcessRunning("wgserver");
        }

        /// <summary>
        /// Force-kills all instances of a process and closes any error dialogs.
        /// Use with caution - only for cleanup after crashes.
        /// </summary>
        /// <param name="processName">Name of the process without .exe extension</param>
        /// <returns>Number of processes killed</returns>
        public static int ForceKillProcess(string processName)
        {
            int killedCount = 0;

            try
            {
                // Remove .exe extension if present
                if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    processName = processName.Substring(0, processName.Length - 4);

                Process[] processes = Process.GetProcessesByName(processName);

                foreach (var process in processes)
                {
                    try
                    {
                        // Close any dialogs for this process first
                        Core.DialogCloser.CloseDialogsForProcess(process.Id);

                        // Give it a moment to close gracefully
                        System.Threading.Thread.Sleep(100);

                        // Force kill if still running
                        if (!process.HasExited)
                        {
                            process.Kill();
                            killedCount++;
                        }

                        process.Dispose();
                    }
                    catch
                    {
                        // Continue with other processes if one fails
                    }
                }
            }
            catch
            {
                // Ignore errors - this is cleanup code
            }

            return killedCount;
        }

        /// <summary>
        /// Cleans up all BBS-related processes and dialogs.
        /// Useful before restarting the BBS.
        /// </summary>
        public static void CleanupBBSProcesses()
        {
            try
            {
                // Close all BBS error dialogs
                Core.DialogCloser.CloseAllBBSDialogs();

                // Small delay
                System.Threading.Thread.Sleep(200);

                // Force kill any lingering wgserver processes
                ForceKillProcess("wgserver");

                // Force kill any lingering wgsappgo processes
                ForceKillProcess("wgsappgo");
            }
            catch
            {
                // Ignore errors - this is cleanup code
            }
        }
    }
}
