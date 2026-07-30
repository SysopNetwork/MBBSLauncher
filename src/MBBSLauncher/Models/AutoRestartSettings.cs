// MBBSLauncher - BBS Monitoring / Auto-Restart Settings
// Created by Mark Laudenbach with Love in Iowa
// https://github.com/SysopNetwork/MBBSLauncher
//
// File: Models/AutoRestartSettings.cs
// Version: v2.0
//
// Change History:
// 26.02.07.1 - Initial creation (Auto Restart Settings - disabled placeholder in v1.5)
// 26.06.22.1 - v2.0 - Reworked into the BBS Monitoring settings model. Backed by the [Monitoring]
//                     INI section. Two sysop-definable stages: Stage 1 restarts the BBS software
//                     (1-3 attempts), Stage 2 reboots the machine once. Depends on the TICKLER
//                     module (https://github.com/SysopNetwork/TICKLER) which writes TICKLER.RUN to
//                     the BBS root while running and deletes it on a clean shutdown/cleanup.

namespace MBBSLauncher.Models
{
    /// <summary>
    /// Settings for the BBS Monitoring &amp; Auto-Restart feature (the [Monitoring] INI section).
    /// </summary>
    public class AutoRestartSettings
    {
        // --- Stage 1: restart the BBS software ---

        /// <summary>Master switch / Stage 1 enable. When off, no monitoring or auto-restart happens.</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>Number of times to try restarting the BBS software before giving up (1-3).</summary>
        public int RestartAttempts { get; set; } = 3;

        /// <summary>Cancelable countdown shown before each restart attempt (seconds).</summary>
        public int RestartCountdownSeconds { get; set; } = 30;

        // --- Stage 2: reboot the machine ---

        /// <summary>Stage 2 enable. If Stage 1 is exhausted and this is on, reboot the computer once.</summary>
        public bool RebootEnabled { get; set; } = false;

        /// <summary>Cancelable countdown shown before the machine reboot (seconds).</summary>
        public int RebootCountdownSeconds { get; set; } = 30;

        // --- Timing knobs ---

        /// <summary>How long the crash condition must persist before we act (debounce, seconds).</summary>
        public int CrashConfirmSeconds { get; set; } = 10;

        /// <summary>How long to wait for the BBS to come back up after a restart before calling it a failure (seconds).</summary>
        public int StartupGraceSeconds { get; set; } = 60;

        /// <summary>Delay between restart attempts (seconds).</summary>
        public int AttemptDelaySeconds { get; set; } = 15;

        /// <summary>How long the BBS must run healthy before the one-shot reboot guard resets (minutes).</summary>
        public int HealthyResetMinutes { get; set; } = 10;

        /// <summary>
        /// Loads monitoring settings from the [Monitoring] section of the config, clamping to safe ranges.
        /// </summary>
        public static AutoRestartSettings LoadFromConfig(ConfigManager config)
        {
            return new AutoRestartSettings
            {
                Enabled                 = config.GetBool("Monitoring", "Enabled", false),
                RestartAttempts         = Clamp(config.GetInt("Monitoring", "RestartAttempts", 3), 1, 3),
                RestartCountdownSeconds = Clamp(config.GetInt("Monitoring", "RestartCountdownSeconds", 30), 0, 300),
                RebootEnabled           = config.GetBool("Monitoring", "RebootEnabled", false),
                RebootCountdownSeconds  = Clamp(config.GetInt("Monitoring", "RebootCountdownSeconds", 30), 5, 600),
                CrashConfirmSeconds     = Clamp(config.GetInt("Monitoring", "CrashConfirmSeconds", 10), 2, 120),
                StartupGraceSeconds     = Clamp(config.GetInt("Monitoring", "StartupGraceSeconds", 60), 10, 600),
                AttemptDelaySeconds     = Clamp(config.GetInt("Monitoring", "AttemptDelaySeconds", 15), 0, 300),
                HealthyResetMinutes     = Clamp(config.GetInt("Monitoring", "HealthyResetMinutes", 10), 1, 120)
            };
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
