// MBBSLauncher - Single Instance Manager
// Created by Mark Laudenbach with Love in Iowa
// https://github.com/SysopNetwork/MBBSLauncher
//
// File: Core/SingleInstanceManager.cs
// Version: v1.85
//
// Change History:
// 26.02.06.1 - Initial creation for v1.5
// 26.06.04.1 - v1.85 - FindExistingWindow now uses EnumWindows prefix scan instead of
//              hard-coded per-version title strings; also finds hidden/tray windows

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace MBBSLauncher.Core
{
    /// <summary>
    /// Manages single instance enforcement using a named mutex.
    /// Prevents multiple instances of the launcher from running simultaneously.
    /// </summary>
    public static class SingleInstanceManager
    {
        private static Mutex? _instanceMutex;
        private const string MUTEX_NAME = "Global\\MBBSLauncher_SingleInstance_E4F2A1B9";
        private const string WINDOW_TITLE_PREFIX = "MBBSLauncher";

        #region Win32 API Imports

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool FlashWindow(IntPtr hWnd, bool bInvert);

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;

        #endregion

        /// <summary>
        /// Attempts to acquire the single instance mutex.
        /// Returns true if this is the first instance, false if another instance is already running.
        /// </summary>
        public static bool AcquireInstance()
        {
            try
            {
                _instanceMutex = new Mutex(true, MUTEX_NAME, out bool createdNew);
                return createdNew;
            }
            catch (Exception ex)
            {
                // If mutex creation fails, log and allow instance to run
                // Better to allow duplicate than to block legitimate use
                System.Windows.Forms.MessageBox.Show(
                    $"Warning: Could not check for existing instance.\n\n{ex.Message}\n\nContinuing anyway...",
                    "Single Instance Check Failed",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Warning);
                return true; // Allow this instance to run
            }
        }

        /// <summary>
        /// Attempts to restore an existing launcher window to the foreground.
        /// Returns true if an existing window was found and restored.
        /// </summary>
        public static bool RestoreExistingInstance()
        {
            try
            {
                // Try to find existing launcher window by title
                IntPtr hWnd = FindExistingWindow();

                if (hWnd != IntPtr.Zero)
                {
                    // Window found - restore and activate it
                    if (IsIconic(hWnd))
                    {
                        ShowWindow(hWnd, SW_RESTORE);
                    }
                    else
                    {
                        ShowWindow(hWnd, SW_SHOW);
                    }

                    SetForegroundWindow(hWnd);

                    // Flash window to get user's attention
                    FlashWindow(hWnd, true);

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                // Log error but don't prevent new instance from starting
                System.Windows.Forms.MessageBox.Show(
                    $"Could not restore existing window:\n\n{ex.Message}\n\nStarting new instance...",
                    "Restore Failed",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Warning);
                return false;
            }
        }

        /// <summary>
        /// Finds the existing launcher window handle by scanning all top-level windows
        /// (including hidden/tray windows) for a title that starts with "MBBSLauncher".
        /// This is version-agnostic so it works across all releases without hardcoded title strings.
        /// </summary>
        private static IntPtr FindExistingWindow()
        {
            IntPtr found = IntPtr.Zero;

            EnumWindows((hWnd, lParam) =>
            {
                var sb = new System.Text.StringBuilder(256);
                if (GetWindowText(hWnd, sb, 256) > 0)
                {
                    if (sb.ToString().StartsWith(WINDOW_TITLE_PREFIX, StringComparison.OrdinalIgnoreCase))
                    {
                        found = hWnd;
                        return false; // stop enumeration
                    }
                }
                return true; // continue enumeration
            }, IntPtr.Zero);

            return found;
        }

        /// <summary>
        /// Releases the single instance mutex.
        /// Should be called when the application exits.
        /// </summary>
        public static void Release()
        {
            try
            {
                if (_instanceMutex != null)
                {
                    _instanceMutex.ReleaseMutex();
                    _instanceMutex.Dispose();
                    _instanceMutex = null;
                }
            }
            catch (Exception ex)
            {
                // Log but don't throw - we're likely shutting down anyway
                Debug.WriteLine($"Error releasing instance mutex: {ex.Message}");
            }
        }
    }
}
