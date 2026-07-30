// MBBSLauncher - Program Option Model
// Created by Mark Laudenbach with Love in Iowa
// https://github.com/laudenbachm/MBBS-Launcher
//
// File: Models/ProgramOption.cs
// Version: v1.5
//
// Change History:
// 26.02.06.1 - Initial creation for v1.5

using System.IO;

namespace MBBSLauncher.Models
{
    /// <summary>
    /// Represents a menu option program configuration.
    /// </summary>
    public class ProgramOption
    {
        /// <summary>
        /// Option number (1-8, 99, etc.).
        /// </summary>
        public int OptionNumber { get; set; }

        /// <summary>
        /// Display name of the option.
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
        /// Checks if the program option is valid (has a name and existing executable).
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Name) &&
                   !string.IsNullOrWhiteSpace(Path) &&
                   File.Exists(Path);
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
