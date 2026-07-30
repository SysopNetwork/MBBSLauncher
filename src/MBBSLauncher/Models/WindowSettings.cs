// MBBSLauncher - Window Settings Model
// Created by Mark Laudenbach with Love in Iowa
// https://github.com/laudenbachm/MBBS-Launcher
//
// File: Models/WindowSettings.cs
// Version: v1.5
//
// Change History:
// 26.02.06.1 - Initial creation for v1.5

using System.Drawing;

namespace MBBSLauncher.Models
{
    /// <summary>
    /// Window position and size settings.
    /// </summary>
    public class WindowSettings
    {
        /// <summary>
        /// X position of the window.
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Y position of the window.
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// Window width.
        /// </summary>
        public int Width { get; set; } = 960;

        /// <summary>
        /// Window height.
        /// </summary>
        public int Height { get; set; } = 540;

        /// <summary>
        /// Gets the window location as a Point.
        /// </summary>
        public Point Location => new Point(X, Y);

        /// <summary>
        /// Gets the window size as a Size.
        /// </summary>
        public Size Size => new Size(Width, Height);
    }
}
