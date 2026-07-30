// MBBSLauncher - TICKLER.RUN Monitor
// Created by Mark Laudenbach with Love in Iowa
// https://github.com/SysopNetwork/MBBSLauncher
//
// File: Core/TicklerMonitor.cs
// Version: v2.0
//
// Change History:
// 26.06.22.1 - v2.0 - Initial creation. Reads the TICKLER.RUN heartbeat file the SNTICKLR module
//                     writes to the BBS root folder. The module creates TICKLER.RUN when the BBS
//                     comes up and DELETES it on a clean shutdown or cleanup. So: BBS not running
//                     + TICKLER.RUN still present = the BBS crashed (it did not shut down cleanly).
// 26.06.30.1 - v2.0-beta6 - Added install/uninstall helpers: InstallModule() copies SNTICKLR.DLL
//                     and SNTICKLR.MDF from the MBBS Module folder beside the launcher into the BBS
//                     folder; UninstallModule() removes them. Source files are never deleted.
// 26.06.30.2 - v2.0-beta9 - SNTICKLR.DLL and SNTICKLR.MDF are now embedded in the launcher exe
//                     as manifest resources; the MBBS Module\ folder is no longer needed in the
//                     release. InstallModule() extracts from the embedded resource instead of
//                     copying from disk. Added ModuleShipVersion / ModuleMinVersion constants,
//                     GetInstalledVersion(), and IsUpdateNeeded() for version-aware update logic.
//                     ModuleSourceFolder and SourceFilesExist() removed.

using System;
using System.IO;
using System.Reflection;

namespace MBBSLauncher.Core
{
    /// <summary>
    /// Helper for reading the SNTICKLR module's TICKLER.RUN heartbeat file, and for installing
    /// or uninstalling the SNTICKLR module files into/from the BBS folder.
    /// </summary>
    public static class TicklerMonitor
    {
        /// <summary>Name of the heartbeat file the SNTICKLR module writes to the BBS root.</summary>
        public const string TicklerFileName = "TICKLER.RUN";

        private const string DllFileName = "SNTICKLR.DLL";
        private const string MdfFileName = "SNTICKLR.MDF";
        private const string EmbeddedDllResource = "MBBSLauncher.SNTICKLR.DLL";
        private const string EmbeddedMdfResource = "MBBSLauncher.SNTICKLR.MDF";

        /// <summary>
        /// Version of SNTICKLR bundled with this launcher build. When the installed version in
        /// the BBS folder is older than this, IsUpdateNeeded() returns true and the launcher
        /// will replace it on next startup while the BBS is down.
        /// </summary>
        public const string ModuleShipVersion = "1.0.0.0";

        /// <summary>
        /// Oldest SNTICKLR version this launcher can work with correctly. Installed versions
        /// at or above this threshold are compatible even if newer than ModuleShipVersion.
        /// </summary>
        public const string ModuleMinVersion = "1.0.0.0";

        /// <summary>
        /// Returns the file version of SNTICKLR.DLL currently installed in the BBS folder.
        /// Returns Version(0,0,0,0) if the file is missing, unversioned, or unreadable.
        /// </summary>
        public static Version GetInstalledVersion(string bbsPath)
        {
            try
            {
                string dllPath = Path.Combine(bbsPath, DllFileName);
                if (!File.Exists(dllPath)) return new Version(0, 0, 0, 0);
                var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(dllPath);
                if (!string.IsNullOrWhiteSpace(info.FileVersion) &&
                    Version.TryParse(info.FileVersion, out var v))
                    return v;
            }
            catch { }
            return new Version(0, 0, 0, 0);
        }

        /// <summary>
        /// Returns true when the BBS folder is missing SNTICKLR files, or the installed
        /// version is older than ModuleShipVersion. Never returns true just to downgrade
        /// a newer-than-shipped version.
        /// </summary>
        public static bool IsUpdateNeeded(string bbsPath)
        {
            if (!File.Exists(Path.Combine(bbsPath, DllFileName)) ||
                !File.Exists(Path.Combine(bbsPath, MdfFileName)))
                return true;

            Version installed = GetInstalledVersion(bbsPath);
            Version ship = Version.Parse(ModuleShipVersion);
            return installed < ship;
        }

        /// <summary>
        /// Extracts SNTICKLR.DLL and SNTICKLR.MDF from the embedded launcher resources and
        /// writes them to the BBS folder, overwriting any existing files.
        /// Returns true on success; outError contains a human-readable message on failure.
        /// </summary>
        public static bool InstallModule(string bbsPath, out string? outError)
        {
            outError = null;
            try
            {
                var assembly = Assembly.GetExecutingAssembly();

                using (var src = assembly.GetManifestResourceStream(EmbeddedDllResource))
                {
                    if (src == null)
                    {
                        outError = "SNTICKLR.DLL could not be read from the launcher executable. The file may be corrupt.";
                        return false;
                    }
                    using var dst = File.Create(Path.Combine(bbsPath, DllFileName));
                    src.CopyTo(dst);
                }

                using (var src = assembly.GetManifestResourceStream(EmbeddedMdfResource))
                {
                    if (src == null)
                    {
                        outError = "SNTICKLR.MDF could not be read from the launcher executable. The file may be corrupt.";
                        return false;
                    }
                    using var dst = File.Create(Path.Combine(bbsPath, MdfFileName));
                    src.CopyTo(dst);
                }

                return true;
            }
            catch (Exception ex)
            {
                outError = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Removes SNTICKLR.DLL and SNTICKLR.MDF from the BBS folder.
        /// Returns true on success; outError contains a human-readable message on failure.
        /// </summary>
        public static bool UninstallModule(string bbsPath, out string? outError)
        {
            outError = null;
            try
            {
                string dstDll = Path.Combine(bbsPath, DllFileName);
                string dstMdf = Path.Combine(bbsPath, MdfFileName);

                if (File.Exists(dstDll)) File.Delete(dstDll);
                if (File.Exists(dstMdf)) File.Delete(dstMdf);
                return true;
            }
            catch (Exception ex)
            {
                outError = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Returns the full path to TICKLER.RUN for the given BBS root folder,
        /// or null if no BBS path is configured.
        /// </summary>
        public static string? GetTicklerPath(string? bbsPath)
        {
            if (string.IsNullOrWhiteSpace(bbsPath))
                return null;
            return Path.Combine(bbsPath, TicklerFileName);
        }

        /// <summary>
        /// True if TICKLER.RUN exists in the BBS root folder. A present file while the BBS
        /// process is not running indicates a crash (the module deletes it on clean shutdown).
        /// </summary>
        public static bool Exists(string? bbsPath)
        {
            string? path = GetTicklerPath(bbsPath);
            if (path == null) return false;
            try
            {
                return File.Exists(path);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Attempts to read the TIMESTAMP value (when the BBS came up) from TICKLER.RUN.
        /// Returns null if the file is missing or the timestamp can't be parsed.
        /// </summary>
        public static DateTime? ReadStartTimestamp(string? bbsPath)
        {
            string? path = GetTicklerPath(bbsPath);
            if (path == null) return null;
            try
            {
                if (!File.Exists(path)) return null;

                foreach (var rawLine in File.ReadAllLines(path))
                {
                    string line = rawLine.Trim();
                    if (line.StartsWith("TIMESTAMP=", StringComparison.OrdinalIgnoreCase))
                    {
                        string value = line.Substring("TIMESTAMP=".Length).Trim();
                        if (DateTime.TryParse(value, out DateTime ts))
                            return ts;
                    }
                }
            }
            catch
            {
                // Unreadable / locked — treat as no timestamp
            }
            return null;
        }
    }
}
