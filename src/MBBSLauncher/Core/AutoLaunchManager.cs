// MBBSLauncher - Auto-Launch Manager
// Created by Mark Laudenbach with Love in Iowa
// https://github.com/SysopNetwork/MBBSLauncher
//
// File: Core/AutoLaunchManager.cs
// Version: v1.55
//
// Change History:
// 26.02.06.1 - Initial creation for v1.5
// 26.02.18.1 - v1.55 - Skip auto-launch if process is already running

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MBBSLauncher.Models;

namespace MBBSLauncher.Core
{
    /// <summary>
    /// Manages automatic launching of multiple programs after the BBS starts.
    /// Each program has independent delay timer.
    /// </summary>
    public class AutoLaunchManager
    {
        private readonly List<AutoLaunchProgram> _programs;
        private readonly Dictionary<string, CancellationTokenSource> _activeLaunches;
        private bool _isActive;

        // Events for UI updates
        public event EventHandler<AutoLaunchCountdownEventArgs>? CountdownTick;
        public event EventHandler<AutoLaunchEventArgs>? ProgramLaunched;
        public event EventHandler? AllLaunchesCancelled;

        public AutoLaunchManager()
        {
            _programs = new List<AutoLaunchProgram>();
            _activeLaunches = new Dictionary<string, CancellationTokenSource>();
            _isActive = false;
        }

        /// <summary>
        /// Loads auto-launch programs from configuration.
        /// </summary>
        public void LoadFromConfig(ConfigManager config)
        {
            _programs.Clear();

            // Load programs from INI (AutoLaunch1, AutoLaunch2, etc.)
            for (int i = 1; i <= 20; i++) // Support up to 20 programs
            {
                string id = $"AutoLaunch{i}";
                string name = config.GetValue("AutoLaunch", $"{id}Name", "");
                string path = config.GetValue("AutoLaunch", $"{id}Path", "");
                string args = config.GetValue("AutoLaunch", $"{id}Args", "");
                string delayStr = config.GetValue("AutoLaunch", $"{id}Delay", "30");
                string enabledStr = config.GetValue("AutoLaunch", $"{id}Enabled", "false");
                string minimizedStr = config.GetValue("AutoLaunch", $"{id}Minimized", "true");

                // Skip if not configured
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path))
                    continue;

                int delay = int.TryParse(delayStr, out int d) ? d : 30;
                bool enabled = enabledStr.ToLower() == "true";
                bool minimized = minimizedStr.ToLower() == "true";

                var program = new AutoLaunchProgram
                {
                    Id = id,
                    Name = name,
                    Path = path,
                    Arguments = args,
                    DelaySeconds = delay,
                    Enabled = enabled,
                    LaunchMinimized = minimized
                };

                _programs.Add(program);
            }

            LogEvent($"Loaded {_programs.Count} auto-launch programs from config");
        }

        /// <summary>
        /// Saves auto-launch programs to configuration.
        /// </summary>
        public void SaveToConfig(ConfigManager config)
        {
            // Clear existing AutoLaunch entries first
            for (int i = 1; i <= 20; i++)
            {
                string id = $"AutoLaunch{i}";
                config.SetValue("AutoLaunch", $"{id}Name", "");
                config.SetValue("AutoLaunch", $"{id}Path", "");
                config.SetValue("AutoLaunch", $"{id}Args", "");
                config.SetValue("AutoLaunch", $"{id}Delay", "30");
                config.SetValue("AutoLaunch", $"{id}Enabled", "false");
                config.SetValue("AutoLaunch", $"{id}Minimized", "true");
            }

            // Save current programs
            foreach (var program in _programs)
            {
                config.SetValue("AutoLaunch", $"{program.Id}Name", program.Name);
                config.SetValue("AutoLaunch", $"{program.Id}Path", program.Path);
                config.SetValue("AutoLaunch", $"{program.Id}Args", program.Arguments);
                config.SetValue("AutoLaunch", $"{program.Id}Delay", program.DelaySeconds.ToString());
                config.SetValue("AutoLaunch", $"{program.Id}Enabled", program.Enabled.ToString().ToLower());
                config.SetValue("AutoLaunch", $"{program.Id}Minimized", program.LaunchMinimized.ToString().ToLower());
            }

            LogEvent($"Saved {_programs.Count} auto-launch programs to config");
        }

        /// <summary>
        /// Gets all configured programs (enabled and disabled).
        /// </summary>
        public List<AutoLaunchProgram> GetAllPrograms()
        {
            return new List<AutoLaunchProgram>(_programs);
        }

        /// <summary>
        /// Gets only enabled programs.
        /// </summary>
        public List<AutoLaunchProgram> GetEnabledPrograms()
        {
            return _programs.Where(p => p.Enabled).ToList();
        }

        /// <summary>
        /// Adds a new auto-launch program.
        /// </summary>
        public void AddProgram(AutoLaunchProgram program)
        {
            // Assign next available ID
            int nextId = 1;
            while (_programs.Any(p => p.Id == $"AutoLaunch{nextId}"))
                nextId++;

            program.Id = $"AutoLaunch{nextId}";
            _programs.Add(program);
        }

        /// <summary>
        /// Updates an existing auto-launch program.
        /// </summary>
        public bool UpdateProgram(string id, AutoLaunchProgram updatedProgram)
        {
            var existing = _programs.FirstOrDefault(p => p.Id == id);
            if (existing == null)
                return false;

            existing.Name = updatedProgram.Name;
            existing.Path = updatedProgram.Path;
            existing.Arguments = updatedProgram.Arguments;
            existing.DelaySeconds = updatedProgram.DelaySeconds;
            existing.Enabled = updatedProgram.Enabled;

            return true;
        }

        /// <summary>
        /// Removes an auto-launch program.
        /// </summary>
        public bool RemoveProgram(string id)
        {
            var program = _programs.FirstOrDefault(p => p.Id == id);
            if (program == null)
                return false;

            _programs.Remove(program);
            return true;
        }

        /// <summary>
        /// Starts auto-launch countdowns for all enabled programs.
        /// Each program launches independently based on its delay.
        /// </summary>
        public void StartAllLaunches()
        {
            if (_isActive)
            {
                LogEvent("Auto-launch already active - stopping existing launches");
                StopAllLaunches();
            }

            _isActive = true;
            var enabledPrograms = GetEnabledPrograms();

            LogEvent($"Starting auto-launch for {enabledPrograms.Count} programs");

            foreach (var program in enabledPrograms)
            {
                StartProgramLaunch(program);
            }
        }

        /// <summary>
        /// Starts countdown and launch for a single program.
        /// </summary>
        private void StartProgramLaunch(AutoLaunchProgram program)
        {
            var cts = new CancellationTokenSource();
            _activeLaunches[program.Id] = cts;

            Task.Run(async () =>
            {
                try
                {
                    // Countdown from delay to 0
                    for (int remaining = program.DelaySeconds; remaining > 0; remaining--)
                    {
                        if (cts.Token.IsCancellationRequested)
                        {
                            LogEvent($"Countdown cancelled for {program.Name}");
                            return;
                        }

                        // Fire countdown event for UI
                        CountdownTick?.Invoke(this, new AutoLaunchCountdownEventArgs
                        {
                            ProgramId = program.Id,
                            ProgramName = program.Name,
                            SecondsRemaining = remaining,
                            TotalSeconds = program.DelaySeconds
                        });

                        await Task.Delay(1000, cts.Token);
                    }

                    if (cts.Token.IsCancellationRequested)
                        return;

                    // Countdown complete - launch program
                    LaunchProgram(program);
                }
                catch (TaskCanceledException)
                {
                    // Normal cancellation
                    LogEvent($"Launch cancelled for {program.Name}");
                }
                catch (Exception ex)
                {
                    LogEvent($"Error during countdown for {program.Name}: {ex.Message}");
                }
                finally
                {
                    _activeLaunches.Remove(program.Id);
                }
            }, cts.Token);
        }

        /// <summary>
        /// Launches a program without countdown (immediate).
        /// Skips launch if the process is already running on the system.
        /// </summary>
        private void LaunchProgram(AutoLaunchProgram program)
        {
            if (!File.Exists(program.Path))
            {
                LogEvent($"Cannot launch {program.Name} - file not found: {program.Path}");
                ProgramLaunched?.Invoke(this, new AutoLaunchEventArgs
                {
                    ProgramId = program.Id,
                    ProgramName = program.Name,
                    Success = false,
                    ErrorMessage = "File not found"
                });
                return;
            }

            // Check if the process is already running before launching (v1.55)
            // Skip check for .bat files - they run under cmd.exe, not by script name
            bool isBatch = program.Path.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);
            string processName = Path.GetFileNameWithoutExtension(program.Path);
            if (!isBatch && !string.IsNullOrWhiteSpace(processName) && ProcessHelper.IsProcessRunning(processName))
            {
                LogEvent($"Skipping auto-launch of {program.Name} - process '{processName}' is already running");
                ProgramLaunched?.Invoke(this, new AutoLaunchEventArgs
                {
                    ProgramId = program.Id,
                    ProgramName = program.Name,
                    Success = true,
                    ErrorMessage = "Already running - skipped"
                });
                return;
            }

            try
            {
                string? workingDir = Path.GetDirectoryName(program.Path);
                var process = ProcessHelper.LaunchProgram(
                    program.Path,
                    workingDir,
                    string.IsNullOrWhiteSpace(program.Arguments) ? null : program.Arguments,
                    program.LaunchMinimized);

                bool success = (process != null);

                LogEvent($"Launched {program.Name}: {(success ? "SUCCESS" : "FAILED")}");

                ProgramLaunched?.Invoke(this, new AutoLaunchEventArgs
                {
                    ProgramId = program.Id,
                    ProgramName = program.Name,
                    Success = success,
                    ErrorMessage = success ? null : "Failed to start process"
                });
            }
            catch (Exception ex)
            {
                LogEvent($"Error launching {program.Name}: {ex.Message}");

                ProgramLaunched?.Invoke(this, new AutoLaunchEventArgs
                {
                    ProgramId = program.Id,
                    ProgramName = program.Name,
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        /// <summary>
        /// Cancels a specific program's countdown.
        /// </summary>
        public void CancelProgramLaunch(string programId)
        {
            if (_activeLaunches.TryGetValue(programId, out var cts))
            {
                cts.Cancel();
                _activeLaunches.Remove(programId);

                var program = _programs.FirstOrDefault(p => p.Id == programId);
                LogEvent($"Cancelled launch for {program?.Name ?? programId}");
            }
        }

        /// <summary>
        /// Stops all active launch countdowns.
        /// </summary>
        public void StopAllLaunches()
        {
            LogEvent($"Stopping all auto-launches ({_activeLaunches.Count} active)");

            foreach (var cts in _activeLaunches.Values)
            {
                cts.Cancel();
            }

            _activeLaunches.Clear();
            _isActive = false;

            AllLaunchesCancelled?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Gets list of programs currently counting down.
        /// </summary>
        public List<string> GetActiveLaunches()
        {
            return new List<string>(_activeLaunches.Keys);
        }

        /// <summary>
        /// Checks if any launches are currently active.
        /// </summary>
        public bool IsActive => _isActive && _activeLaunches.Count > 0;

        /// <summary>
        /// Logs events to audit.log.
        /// </summary>
        private void LogEvent(string message)
        {
            try
            {
                Program.LogError("AutoLaunchManager", new Exception($"[INFO] {message}"));
            }
            catch
            {
                // If logging fails, continue anyway
            }
        }
    }

    /// <summary>
    /// Event args for auto-launch countdown ticks.
    /// </summary>
    public class AutoLaunchCountdownEventArgs : EventArgs
    {
        public string ProgramId { get; set; } = string.Empty;
        public string ProgramName { get; set; } = string.Empty;
        public int SecondsRemaining { get; set; }
        public int TotalSeconds { get; set; }
    }

    /// <summary>
    /// Event args for program launch attempts.
    /// </summary>
    public class AutoLaunchEventArgs : EventArgs
    {
        public string ProgramId { get; set; } = string.Empty;
        public string ProgramName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
