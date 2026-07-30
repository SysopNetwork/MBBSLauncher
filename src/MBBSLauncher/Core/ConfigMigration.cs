// MBBSLauncher - Configuration Migration
// Created by Mark Laudenbach with Love in Iowa
// https://github.com/laudenbachm/MBBS-Launcher
//
// File: Core/ConfigMigration.cs
// Version: v2.0
//
// Change History:
// 26.02.06.1 - Initial creation for v1.5
// 26.02.19.2 - v1.70 - Moved version marker from [AutoLaunch].Version to [Settings].ConfigVersion
//                       Version written dynamically from Program.APP_VERSION (no more hardcoded "1.5")
//                       NeedsMigration() checks both old and new marker locations for compatibility
//                       Added EnsureVersionMarker() to silently upgrade old-style markers on launch
// 26.06.22.1 - v2.0 - Migration-success message now logs via Program.LogInfo instead of a fake
//                     Exception through LogError, so audit.log no longer shows it as an exception.

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace MBBSLauncher.Core
{
    /// <summary>
    /// Handles migration of v1.20 configuration to current format.
    /// Version marker is stored in [Settings].ConfigVersion.
    /// </summary>
    public static class ConfigMigration
    {
        /// <summary>
        /// Checks if the current configuration needs migration from v1.20.
        /// Returns true ONLY for genuine v1.20 configs (no version marker of any kind).
        /// </summary>
        public static bool NeedsMigration()
        {
            string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MBBSLauncher.ini");

            if (!File.Exists(iniPath))
                return false; // No config file — fresh install, not a migration

            try
            {
                var config = new ConfigManager();

                // New-style marker (v1.70+): [Settings].ConfigVersion
                string newMarker = config.GetValue("Settings", "ConfigVersion");
                if (!string.IsNullOrEmpty(newMarker))
                    return false; // Already current

                // Old-style marker (v1.5–v1.60): [AutoLaunch].Version
                string oldMarker = config.GetValue("AutoLaunch", "Version");
                if (!string.IsNullOrEmpty(oldMarker))
                    return false; // Already migrated from v1.20, just needs marker upgrade

                // No marker at all — genuine v1.20 config
                return true;
            }
            catch
            {
                return false; // If config can't be loaded, don't block startup
            }
        }

        /// <summary>
        /// Ensures the new-style [Settings].ConfigVersion marker is written.
        /// Called on every startup when migration is not needed, so existing installs
        /// that had the old [AutoLaunch].Version marker get silently upgraded.
        /// </summary>
        public static void EnsureVersionMarker()
        {
            string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MBBSLauncher.ini");

            if (!File.Exists(iniPath))
                return;

            try
            {
                var config = new ConfigManager();

                string currentMarker = config.GetValue("Settings", "ConfigVersion");
                if (string.IsNullOrEmpty(currentMarker))
                {
                    // Write new-style marker and clean up old one
                    config.SetValue("Settings", "ConfigVersion", Program.APP_VERSION);
                    config.RemoveValue("AutoLaunch", "Version");
                    config.SaveConfig();
                }
            }
            catch
            {
                // Non-critical — don't block startup if this fails
            }
        }

        /// <summary>
        /// Migrates v1.20 configuration to current format.
        /// Creates a backup before migration.
        /// </summary>
        public static MigrationResult MigrateV120ToV20()
        {
            var result = new MigrationResult();

            try
            {
                string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MBBSLauncher.ini");

                // 1. Create backup
                CreateBackup(iniPath);
                result.BackupPath = iniPath + ".v120.backup";

                // 2. Load v1.20 config
                var v1Config = new ConfigManager();

                // 3. Create new config (in memory)
                var v2Config = new ConfigManager();

                // 4. Migrate all sections
                MigratePaths(v1Config, v2Config, result);
                MigrateWindow(v1Config, v2Config, result);
                MigrateSettings(v1Config, v2Config, result);
                MigratePrograms(v1Config, v2Config, result);

                // 5. Convert Ghost3 to AutoLaunch1
                MigrateGhost3ToAutoLaunch(v1Config, v2Config, result);

                // 6. Add version marker
                AddVersionMarker(v2Config, result);

                // 7. Save new config
                v2Config.SaveConfig();

                result.Success = true;
                Program.LogInfo("Migration", $"Configuration migrated successfully from v1.20 to {Program.APP_VERSION}");

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                Program.LogError("Migration", ex);
                return result;
            }
        }

        private static void CreateBackup(string originalPath)
        {
            string backupPath = originalPath + ".v120.backup";

            try
            {
                // If backup already exists, rename to .old
                if (File.Exists(backupPath))
                {
                    string oldBackupPath = backupPath + ".old";
                    if (File.Exists(oldBackupPath))
                        File.Delete(oldBackupPath);
                    File.Move(backupPath, oldBackupPath);
                }

                // Create new backup
                File.Copy(originalPath, backupPath, overwrite: true);
            }
            catch (Exception ex)
            {
                throw new MigrationException("Could not create configuration backup", ex);
            }
        }

        private static void MigratePaths(ConfigManager v1, ConfigManager v2, MigrationResult result)
        {
            string bbsPath = v1.GetValue("Paths", "BBSPath", @"C:\BBSV10");
            v2.SetValue("Paths", "BBSPath", bbsPath);
            result.MigratedSettings.Add($"Paths.BBSPath = {bbsPath}");
        }

        private static void MigrateWindow(ConfigManager v1, ConfigManager v2, MigrationResult result)
        {
            foreach (var key in new[] { "X", "Y", "Width", "Height" })
            {
                string value = v1.GetValue("Window", key, "");
                if (!string.IsNullOrEmpty(value))
                {
                    v2.SetValue("Window", key, value);
                    result.MigratedSettings.Add($"Window.{key} = {value}");
                }
            }
        }

        private static void MigrateSettings(ConfigManager v1, ConfigManager v2, MigrationResult result)
        {
            var settingsToMigrate = new[]
            {
                "AutoLaunchAtStartup",
                "MinimizeToTray",
                "ShowTrayIcon",
                "EscMinimizesToTray",
                "AutoStartBBS",
                "AutoStartDelay",
                "QuietMode"
            };

            foreach (var key in settingsToMigrate)
            {
                string value = v1.GetValue("Settings", key, "");
                if (!string.IsNullOrEmpty(value))
                {
                    v2.SetValue("Settings", key, value);
                    result.MigratedSettings.Add($"Settings.{key} = {value}");
                }
            }
        }

        private static void MigratePrograms(ConfigManager v1, ConfigManager v2, MigrationResult result)
        {
            // Migrate Options 1-8
            for (int i = 1; i <= 8; i++)
            {
                string path = v1.GetValue("Programs", $"Option{i}", "");
                string name = v1.GetValue("Programs", $"Option{i}Name", "");

                if (!string.IsNullOrEmpty(path))
                {
                    v2.SetValue("Programs", $"Option{i}", path);
                    v2.SetValue("Programs", $"Option{i}Name", name);
                    result.MigratedSettings.Add($"Programs.Option{i} = {name}");
                }
            }

            // Migrate Option99
            string option99 = v1.GetValue("Programs", "Option99", "");
            string option99Name = v1.GetValue("Programs", "Option99Name", "");
            if (!string.IsNullOrEmpty(option99))
            {
                v2.SetValue("Programs", "Option99", option99);
                v2.SetValue("Programs", "Option99Name", option99Name);
                result.MigratedSettings.Add($"Programs.Option99 = {option99Name}");
            }

            // Migrate ModuleEditor
            string moduleEditor = v1.GetValue("Programs", "ModuleEditor", "");
            if (!string.IsNullOrEmpty(moduleEditor))
            {
                v2.SetValue("Programs", "ModuleEditor", moduleEditor);
                result.MigratedSettings.Add("Programs.ModuleEditor");
            }
        }

        private static void MigrateGhost3ToAutoLaunch(ConfigManager v1, ConfigManager v2, MigrationResult result)
        {
            bool ghost3Enabled = v1.GetValue("Settings", "Ghost3Enabled", "false") == "true";

            if (ghost3Enabled)
            {
                string ghost3Path = v1.GetValue("Settings", "Ghost3Path", @"C:\Ghost3\Ghost3.exe");
                string ghost3Delay = v1.GetValue("Settings", "Ghost3Delay", "60");

                // Create AutoLaunch1 from Ghost3 settings
                v2.SetValue("AutoLaunch", "AutoLaunch1Name", "Ghost3");
                v2.SetValue("AutoLaunch", "AutoLaunch1Path", ghost3Path);
                v2.SetValue("AutoLaunch", "AutoLaunch1Args", "");
                v2.SetValue("AutoLaunch", "AutoLaunch1Delay", ghost3Delay);
                v2.SetValue("AutoLaunch", "AutoLaunch1Enabled", "true");

                result.MigratedSettings.Add($"Ghost3 → AutoLaunch1 (delay: {ghost3Delay}s)");
            }
            else
            {
                result.MigratedSettings.Add("Ghost3 was disabled (not migrated)");
            }
        }

        private static void AddVersionMarker(ConfigManager v2, MigrationResult result)
        {
            // Write to [Settings].ConfigVersion using current app version — never hardcoded
            v2.SetValue("Settings", "ConfigVersion", Program.APP_VERSION);
            result.MigratedSettings.Add($"Version marker: {Program.APP_VERSION}");
        }
    }

    /// <summary>
    /// Result of a configuration migration operation.
    /// </summary>
    public class MigrationResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string> MigratedSettings { get; set; } = new List<string>();
        public string? BackupPath { get; set; }
    }

    /// <summary>
    /// Exception thrown when migration fails.
    /// </summary>
    public class MigrationException : Exception
    {
        public MigrationException(string message) : base(message) { }
        public MigrationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
