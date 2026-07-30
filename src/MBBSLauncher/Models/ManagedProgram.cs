// MBBSLauncher - Managed Program Model
// Created by Mark Laudenbach with Love in Iowa
// https://github.com/laudenbachm/MBBS-Launcher
//
// File: Models/ManagedProgram.cs
// Version: v1.90
//
// Change History:
// 26.02.11.1 - Initial creation for App Manager (v1.6 Beta)
// 26.02.19.1 - v1.60 - Neutral status text: "⚠ Stopped" replaces "⚠ CRASHED"
// 26.02.19.2 - v1.70 - Shorten Pending text: emoji caused font-fallback overflow in status label
// 26.06.22.1 - v1.90 - Added CleaningUp status: BBS shows "Cleanup..." while wgserver is cycling
//                      during a cleanup but the wgsappgo supervisor is still alive

using System;
using System.IO;

namespace MBBSLauncher.Models
{
    /// <summary>
    /// Represents a program being monitored by the App Manager.
    /// </summary>
    public class ManagedProgram
    {
        /// <summary>
        /// Display name of the program.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Full path to the executable.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Command-line arguments (optional).
        /// </summary>
        public string Arguments { get; set; } = string.Empty;

        /// <summary>
        /// Process name to check for running status (without .exe).
        /// </summary>
        public string ProcessName { get; set; } = string.Empty;

        /// <summary>
        /// Current status of the program.
        /// </summary>
        public ProgramStatus Status { get; set; } = ProgramStatus.Stopped;

        /// <summary>
        /// Seconds remaining if pending launch.
        /// </summary>
        public int SecondsRemaining { get; set; } = 0;

        /// <summary>
        /// Whether this is the BBS (special handling).
        /// </summary>
        public bool IsBBS { get; set; } = false;

        /// <summary>
        /// Auto-launch program ID (if this is an auto-launch program).
        /// </summary>
        public string? AutoLaunchId { get; set; }

        /// <summary>
        /// Whether this program can be manually stopped.
        /// </summary>
        public bool CanStop => !IsBBS && Status == ProgramStatus.Running;

        /// <summary>
        /// Whether this program can be launched manually.
        /// </summary>
        public bool CanLaunch => Status == ProgramStatus.Stopped || Status == ProgramStatus.Crashed;

        /// <summary>
        /// Whether this program's launch can be cancelled.
        /// </summary>
        public bool CanCancelLaunch => Status == ProgramStatus.Pending;

        /// <summary>
        /// Whether launch minimized.
        /// </summary>
        public bool LaunchMinimized { get; set; } = true;

        /// <summary>
        /// Gets the status display text.
        /// </summary>
        public string GetStatusText()
        {
            return Status switch
            {
                ProgramStatus.Running => "Running",
                ProgramStatus.Stopped => "Stopped",
                ProgramStatus.Crashed => "Crashed",
                ProgramStatus.CleaningUp => "Cleanup...",
                ProgramStatus.Pending => $"Launch {FormatTime(SecondsRemaining)}",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Formats seconds as MM:SS.
        /// </summary>
        private string FormatTime(int seconds)
        {
            int minutes = seconds / 60;
            int secs = seconds % 60;
            return $"{minutes}:{secs:D2}";
        }

        /// <summary>
        /// Gets the process name from the executable path.
        /// </summary>
        public static string GetProcessNameFromPath(string exePath)
        {
            string fileName = System.IO.Path.GetFileNameWithoutExtension(exePath);
            return fileName;
        }
    }

    /// <summary>
    /// Status of a managed program.
    /// </summary>
    public enum ProgramStatus
    {
        Stopped,    // Not running
        Running,    // Currently running
        Pending,    // Countdown active, will launch soon
        Crashed,    // Was running, now stopped
        CleaningUp  // BBS only: wgserver is cycling during a cleanup, supervisor still alive
    }
}
