// MBBSLauncher - Auto-Launch Program Model
// Created by Mark Laudenbach with Love in Iowa
// https://github.com/laudenbachm/MBBS-Launcher
//
// File: Models/AutoLaunchProgram.cs
// Version: v1.5
//
// Change History:
// 26.02.06.1 - Initial creation for v1.5

using System.IO;

namespace MBBSLauncher.Models
{
    /// <summary>
    /// Represents a program configured to auto-launch after the BBS starts.
    /// </summary>
    public class AutoLaunchProgram
    {
        /// <summary>
        /// Unique identifier (e.g., "AutoLaunch1", "AutoLaunch2").
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the program (e.g., "Ghost3", "Telnet Server").
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
        /// Delay in seconds before launching (after BBS starts).
        /// </summary>
        public int DelaySeconds { get; set; } = 30;

        /// <summary>
        /// Whether this program is enabled for auto-launch.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Whether to launch this program minimized (doesn't steal focus).
        /// </summary>
        public bool LaunchMinimized { get; set; } = true;

        /// <summary>
        /// Checks if the program configuration is valid.
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Name) &&
                   !string.IsNullOrWhiteSpace(Path) &&
                   File.Exists(Path) &&
                   DelaySeconds >= 0;
        }

        /// <summary>
        /// Gets the full command string (path + arguments).
        /// </summary>
        public string GetFullCommand()
        {
            return string.IsNullOrWhiteSpace(Arguments)
                ? Path
                : $"{Path} {Arguments}";
        }
    }
}
