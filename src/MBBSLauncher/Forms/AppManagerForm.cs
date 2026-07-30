// MBBSLauncher - App Manager Form
// Created by Mark Laudenbach with Love in Iowa
// https://github.com/SysopNetwork/MBBSLauncher
//
// File: Forms/AppManagerForm.cs
// Version: v2.0-beta22
//
// Change History:
// 26.02.11.1 - Initial creation for v1.6 Beta
// 26.02.19.1 - v1.60 - Layout fix: added blank-line top padding in program list to prevent clipping
// 26.02.19.2 - v1.70 - Added opacity/transparency slider with INI persistence
// 26.02.19.3 - v1.70 - Fix: SaveSettings guarded with _initialized flag to prevent Opacity=0 being written during init
// 26.02.19.5 - v1.70 - Resizable form: Anchor-based layout, WndProc bottom-edge resize, LastHeight INI persistence
// 26.02.19.6 - v1.70 - Fix countdown timer not showing: solid BackColor + DPI-aware label sizing
//                      Designer controls are auto-scaled by AutoScaleMode.Font; runtime controls are not.
//                      At 150%+ DPI "Launch 0:30" overflows fixed-width labels, clipping the time.
//                      Fix: compute DeviceDpi/96 scale factor and apply to all row coordinates.
// 26.02.19.8 - v1.70 - Fix status label truncation: "Launch" wraps at space, hiding "0:30".
//                      Root cause: 110px status label + Consolas 11pt + "Launch 0:30" = wrap at space.
//                      Fix: widen form to 310px, increase statusW base from 110 to 140.
// 26.02.19.7 - v1.70 - Fix BBS showing Crashed instead of Stopped on clean shutdown
// 26.06.04.1 - v1.80 - BBSStopDelay: wait N seconds (from [Settings] BBSStopDelay) before restoring
//                      launcher after BBS stops. Cancelled automatically if BBS restarts during the
//                      window. BBSStopDelay=0 restores immediately (default, backwards compatible).
// 26.06.04.2 - v1.80 - Removed dead _bbsWasRunning field (set every tick, never read)
// 26.06.04.3 - v1.80 - Fixed font leak: reuse class-level Font objects instead of creating new Font
//                      per label on every UpdateDisplay call (prior code leaked GDI handles each call)
// 26.06.04.4 - v1.80 - Pause _updateTimer when form is hidden; resume on Show to avoid firing
//                      BBSStopped while user has intentionally hidden the App Manager window
// 26.06.04.5 - v1.85 - Fix: OnFormClosing user-close path now calls CancelBBSStopDelay() so a running
//                      delay timer cannot fire BBSCrashed after the user deliberately hides the window
// 26.06.04.6 - v1.85 - _updateTimer now runs continuously regardless of form visibility so the launcher
//                      always restores when the BBS stops (even when App Manager is hidden)
// 26.06.04.7 - v1.85 - Fixed cancel-button font leak: _cancelButtonFont is now a class-level field
//                      so it is reused across UpdateCancelButton calls instead of being leaked each time
// 26.06.22.1 - v1.90 - Cleanup-aware BBS stop detection. The wgsappgo supervisor stays alive across a
//                      BBS cleanup while wgserver.exe exits and restarts (any duration). The launcher
//                      now only restores when wgsappgo is ALSO gone (a real shutdown); while wgserver
//                      cycles and the supervisor is alive the BBS shows "Cleanup..." and the launcher
//                      stays hidden. Replaces the duration-based guess that popped the launcher up mid-cleanup.
// 26.06.22.2 - v1.90 - Removed the manual BBSStopDelay timer (StartBBSStopDelay/CancelBBSStopDelay and
//                      _bbsStopDelayTimer). Now that cleanup is detected automatically the delay is
//                      obsolete; a real shutdown restores the launcher immediately via OnBBSStopped().
// 26.06.22.3 - v2.0  - BBS Monitoring: UpdateBBSStatus now uses the TICKLER.RUN heartbeat to tell a
//                      crash (file still present while server is down) from a clean shutdown/cleanup.
//                      On a crash with monitoring enabled it shows "Crashed" and leaves recovery +
//                      restore to the RestartManager (no launcher pop-up).
// 26.06.30.1 - v2.0-beta10 - Fix: Always-on-top and Auto-hide checkboxes now reflect saved INI
//                      values on startup instead of always showing the designer defaults.
//                      Fix: CloseButton_Click now calls SaveSettings() so the window position is
//                      persisted whenever the App Manager is hidden, not only at app shutdown.
// 26.07.05.1 - v2.0-beta16 - Cleanup-safety fix (companion to the beta15 RestartManager fix):
//                      UpdateBBSStatus now checks the wgsappgo supervisor BEFORE consulting TICKLER.RUN.
//                      At the start of a cleanup wgserver exits a moment before the TICKLER module deletes
//                      TICKLER.RUN, while wgsappgo keeps running; the old order flipped the BBS to Crashed
//                      in that window and (with monitoring disabled) popped the launcher up mid-cleanup.
//                      A crash now requires wgserver down AND wgsappgo gone AND TICKLER.RUN present, matching
//                      RestartManager.CrashCondition(). While the supervisor is alive it holds at CleaningUp.
// 26.07.05.2 - v2.0-beta17 - Spurious mid-cleanup restore hardening (sysop reported the launcher still
//                      popping up during a cleanup with monitoring OFF and no TICKLER.RUN present):
//                      (1) Debounce the "both processes down" decision — require BBS_DOWN_CONFIRM_TICKS
//                      consecutive ticks with wgserver AND wgsappgo both gone before restoring, so a
//                      cleanup that briefly bounces the Go! supervisor no longer reads as a shutdown;
//                      the streak resets the instant either process reappears, and the row holds at
//                      CleaningUp meanwhile. (2) Log the exact signal state (wgserver/wgsappgo/tickler
//                      present + the resolved TICKLER.RUN path) to audit.log whenever the launcher is
//                      restored, so a misconfigured [Paths] BBSPath (checking the wrong folder for
//                      TICKLER.RUN) is visible instead of silent.
// 26.07.29.1 - v2.0-beta22 - Faster launcher restore after a BBS shutdown (was ~6-8s, now ~1.5-2s):
//                      (1) The BBS is now checked on its own 500ms timer (_bbsTimer) instead of riding
//                      the 2s program-list timer, removing up to 2s of pure polling latency. The
//                      program-list loop skips the BBS row so process enumeration is not multiplied
//                      for every other managed program.
//                      (2) The "both processes down" debounce is now measured in WALL-CLOCK time with
//                      a Stopwatch (_bbsDownClock) instead of counting ticks. Tick counting silently
//                      coupled the safety margin to the timer interval — changing one changed the
//                      other. Default 1500ms, tunable via [Settings] BBSDownConfirmMs (500-30000) so
//                      the value can be measured against a real cleanup without a rebuild.
//                      (3) If the supervisor reappears after we already decided "Stopped" (the retract
//                      case in MainForm), the row returns to Cleanup... instead of sitting at Stopped
//                      for the rest of the cleanup.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MBBSLauncher.Core;
using MBBSLauncher.Models;

namespace MBBSLauncher.Forms
{
    /// <summary>
    /// App Manager - Shows real-time status of BBS and auto-launch programs.
    /// Displays countdowns, running status, and crash detection.
    /// </summary>
    public partial class AppManagerForm : Form
    {
        #region Win32 API for Dragging

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;
        private const int WM_NCHITTEST = 0x84;
        private const int HTBOTTOM = 15;

        #endregion

        // The Major BBS / Worldgroup supervisor process ("Go!" launcher). It stays running for the
        // entire BBS session — including across cleanup cycles where wgserver.exe exits and restarts —
        // and only exits on a genuine shutdown. We use it to tell a cleanup apart from a real stop.
        private const string BBS_SUPERVISOR_PROCESS = "wgsappgo";

        private readonly AutoLaunchManager _autoLaunchManager;
        private readonly ConfigManager _config;
        private readonly List<ManagedProgram> _programs;
        private readonly Dictionary<string, Label> _statusLabels;
        private readonly Timer _updateTimer;
        private readonly Timer _bbsTimer;
        private readonly Panel _programListPanel;
        private Button? _cancelButton;

        // Configuration
        private bool _alwaysOnTop;
        private bool _autoHide;
        private bool _initialized = false; // Guard to prevent SaveSettings during init
        private int _lastContentHeight = 130; // Y position after last program row, used to place cancel button

        // The BBS is polled on its own fast timer so a real shutdown is noticed within half a second.
        // The general program-list timer stays at 2s — it enumerates processes for every managed
        // program and there is no reason to do that four times as often.
        private const int BBS_POLL_INTERVAL_MS = 500;

        // Debounce for the "both processes down" shutdown/crash decision. wgserver exits and wgsappgo
        // may briefly disappear during a cleanup; a single sample where both look gone must NOT be
        // treated as a stop. Both processes must stay gone for this long (and, for a crash, TICKLER.RUN
        // must be present) before we restore the launcher. The clock resets the moment either process
        // reappears.
        //
        // This is measured in WALL-CLOCK milliseconds, not ticks: a tick count silently ties the safety
        // margin to the poll interval, so changing the interval changes the margin without anyone
        // noticing. It is configurable ([Settings] BBSDownConfirmMs) so it can be tuned against a real
        // cleanup on a live board without a rebuild — MainForm's retract watch (see beta22) is the
        // safety net if this proves too short on some setup.
        private const int BBS_DOWN_CONFIRM_MS_DEFAULT = 1500;
        private const int BBS_DOWN_CONFIRM_MS_MIN = 500;
        private const int BBS_DOWN_CONFIRM_MS_MAX = 30000;
        private int _bbsDownConfirmMs = BBS_DOWN_CONFIRM_MS_DEFAULT;
        private readonly System.Diagnostics.Stopwatch _bbsDownClock = new System.Diagnostics.Stopwatch();

        // Shared fonts — created once, reused to avoid GDI handle leaks
        private readonly Font _boldFont = new Font("Consolas", 11, FontStyle.Bold);
        private readonly Font _regularFont = new Font("Consolas", 11, FontStyle.Regular);
        private readonly Font _cancelButtonFont = new Font("Consolas", 9, FontStyle.Bold);

        public AppManagerForm(AutoLaunchManager autoLaunchManager, ConfigManager config)
        {
            _autoLaunchManager = autoLaunchManager;
            _config = config;
            _programs = new List<ManagedProgram>();
            _statusLabels = new Dictionary<string, Label>();

            InitializeComponent();

            // Create program list panel — anchored to all 4 sides so it stretches when user resizes form.
            // Width uses the form's CURRENT ClientSize (already DPI-scaled by AutoScaleMode.Font)
            // so the panel fills the available width at any DPI rather than being pinned to 260px.
            _programListPanel = new Panel
            {
                Location = new Point(10, 35),
                Size = new Size(this.ClientSize.Width - 20, 140),
                BackColor = Color.Transparent,
                AutoScroll = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            this.Controls.Add(_programListPanel);

            // Subscribe to auto-launch events
            _autoLaunchManager.CountdownTick += OnCountdownTick;
            _autoLaunchManager.ProgramLaunched += OnProgramLaunched;
            _autoLaunchManager.AllLaunchesCancelled += OnAllLaunchesCancelled;

            // Update timer (check running status every 2 seconds)
            _updateTimer = new Timer { Interval = 2000 };
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();

            // Dedicated fast timer for the BBS row only — this is what makes the launcher reappear
            // ~1.5-2s after a shutdown instead of ~6-8s. Runs continuously (like _updateTimer) so the
            // restore still happens when the App Manager window is hidden.
            _bbsTimer = new Timer { Interval = BBS_POLL_INTERVAL_MS };
            _bbsTimer.Tick += BbsTimer_Tick;
            _bbsTimer.Start();

            LoadSettings();
            _initialized = true; // Allow SaveSettings from here on
            InitializePrograms();
            UpdateDisplay();
        }

        /// <summary>
        /// Loads configuration settings.
        /// </summary>
        private void LoadSettings()
        {
            // Load window position
            int x = _config.GetInt("AppManager", "LastX", Screen.PrimaryScreen!.WorkingArea.Width - this.Width - 20);
            int y = _config.GetInt("AppManager", "LastY", 20);
            this.Location = new Point(x, y);

            // Load saved height (resizable form)
            int lastHeight = _config.GetInt("AppManager", "LastHeight", 300);
            lastHeight = Math.Clamp(lastHeight, this.MinimumSize.Height, 800);
            this.ClientSize = new Size(this.ClientSize.Width, lastHeight);

            // Load options
            _alwaysOnTop = _config.GetBool("AppManager", "AlwaysOnTop", true);
            _autoHide = _config.GetBool("AppManager", "AutoHide", false);

            // How long wgserver AND wgsappgo must both stay gone before we call it a shutdown.
            _bbsDownConfirmMs = Math.Clamp(
                _config.GetInt("Settings", "BBSDownConfirmMs", BBS_DOWN_CONFIRM_MS_DEFAULT),
                BBS_DOWN_CONFIRM_MS_MIN, BBS_DOWN_CONFIRM_MS_MAX);

            // Reflect saved values in the checkboxes. CheckedChanged fires here but SaveSettings()
            // is guarded by _initialized so nothing is written back during init.
            alwaysOnTopCheckbox.Checked = _alwaysOnTop;
            autoHideCheckbox.Checked = _autoHide;

            // Load opacity (20–100, default 60)
            int opacityPct = _config.GetInt("AppManager", "Opacity", 60);
            opacityPct = Math.Clamp(opacityPct, 20, 100);
            opacityTrackBar.Value = opacityPct;
            opacityValueLabel.Text = $"{opacityPct}%";
            this.Opacity = opacityPct / 100.0;

            this.TopMost = _alwaysOnTop;
        }

        /// <summary>
        /// Saves configuration settings.
        /// </summary>
        private void SaveSettings()
        {
            if (!_initialized) return; // Don't save during InitializeComponent/LoadSettings

            _config.SetValue("AppManager", "LastX", this.Location.X.ToString());
            _config.SetValue("AppManager", "LastY", this.Location.Y.ToString());
            _config.SetValue("AppManager", "LastHeight", this.ClientSize.Height.ToString());
            _config.SetValue("AppManager", "AlwaysOnTop", _alwaysOnTop.ToString().ToLower());
            _config.SetValue("AppManager", "AutoHide", _autoHide.ToString().ToLower());
            _config.SetValue("AppManager", "Opacity", opacityTrackBar.Value.ToString());
            _config.SaveConfig();
        }

        /// <summary>
        /// Initializes the program list.
        /// </summary>
        private void InitializePrograms()
        {
            _programs.Clear();

            // Add BBS
            string bbsPath = _config.GetValue("Paths", "BBSPath", "");
            if (!string.IsNullOrEmpty(bbsPath))
            {
                string bbsExe = Path.Combine(bbsPath, "wgsappgo.exe");
                if (File.Exists(bbsExe))
                {
                    // Initial BBS status: server up => Running; server down but the wgsappgo
                    // supervisor alive => a cleanup is in progress (Cleanup...); otherwise Stopped.
                    ProgramStatus bbsStatus =
                        ProcessHelper.IsProcessRunning("wgserver") ? ProgramStatus.Running :
                        ProcessHelper.IsProcessRunning(BBS_SUPERVISOR_PROCESS) ? ProgramStatus.CleaningUp :
                        ProgramStatus.Stopped;

                    _programs.Add(new ManagedProgram
                    {
                        Name = "The Major BBS",
                        Path = bbsExe,
                        ProcessName = "wgserver", // wgsappgo launches wgserver
                        IsBBS = true,
                        Status = bbsStatus
                    });
                }
            }

            // Add auto-launch programs
            var autoLaunchPrograms = _autoLaunchManager.GetEnabledPrograms();
            foreach (var program in autoLaunchPrograms)
            {
                _programs.Add(new ManagedProgram
                {
                    Name = program.Name,
                    Path = program.Path,
                    Arguments = program.Arguments,
                    ProcessName = ManagedProgram.GetProcessNameFromPath(program.Path),
                    IsBBS = false,
                    AutoLaunchId = program.Id,
                    LaunchMinimized = program.LaunchMinimized,
                    Status = ProcessHelper.IsProcessRunning(
                        ManagedProgram.GetProcessNameFromPath(program.Path))
                        ? ProgramStatus.Running
                        : ProgramStatus.Stopped
                });
            }
        }

        /// <summary>
        /// Updates the display with current program status.
        /// </summary>
        private void UpdateDisplay()
        {
            // Dispose cancel button before clearing panel so UpdateCancelButton creates a fresh one
            if (_cancelButton != null)
            {
                _cancelButton.Dispose();
                _cancelButton = null;
            }
            _programListPanel.Controls.Clear();
            _statusLabels.Clear();

            // Designer controls are auto-scaled by AutoScaleMode.Font at runtime, but controls
            // created in code are NOT. At 150%+ display scale, 11pt Consolas characters are wide
            // enough to overflow fixed pixel widths — "0:30" gets clipped, leaving only "Launch".
            // Fix: scale all coordinates by DeviceDpi/96 so labels grow with the display scale.
            float sf = Math.Max(1f, this.DeviceDpi / 96f);
            int startY  = (int)(22 * sf);
            int rowH    = (int)(26 * sf);
            int labelH  = (int)(24 * sf);
            int nameW   = (int)(140 * sf);
            int statusX = (int)(145 * sf);
            int statusW = (int)(140 * sf);
            int sepW    = (int)(250 * sf);
            int sepGap  = (int)(6  * sf);

            int yPos = startY;

            foreach (var program in _programs)
            {
                // Program name label
                var nameLabel = new Label
                {
                    AutoSize = false,
                    Text = program.Name,
                    Location = new Point(5, yPos),
                    Size = new Size(nameW, labelH),
                    ForeColor = program.IsBBS ? Color.White : Color.LightGray,
                    Font = _boldFont,
                    BackColor = Color.FromArgb(0, 0, 128),
                    Tag = program
                };

                // Right-click menu for non-BBS programs
                if (!program.IsBBS)
                {
                    nameLabel.MouseUp += ProgramLabel_MouseUp;
                    nameLabel.Cursor = Cursors.Hand;
                }

                _programListPanel.Controls.Add(nameLabel);

                // Status label — solid BackColor so changing Text triggers a clean repaint.
                var statusLabel = new Label
                {
                    AutoSize = false,
                    Text = program.GetStatusText(),
                    Location = new Point(statusX, yPos),
                    Size = new Size(statusW, labelH),
                    ForeColor = GetStatusColor(program.Status),
                    Font = _regularFont,
                    BackColor = Color.FromArgb(0, 0, 128),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Tag = program
                };

                _programListPanel.Controls.Add(statusLabel);
                _statusLabels[program.Name] = statusLabel;

                yPos += rowH;

                // Add separator after BBS
                if (program.IsBBS)
                {
                    var separator = new Label
                    {
                        Location = new Point(5, yPos),
                        Size = new Size(sepW, 2),
                        BackColor = Color.Cyan,
                        BorderStyle = BorderStyle.None
                    };
                    _programListPanel.Controls.Add(separator);
                    yPos += sepGap;
                }
            }

            // Track last content Y for cancel button placement within the panel
            _lastContentHeight = yPos;
            UpdateCancelButton();
        }

        /// <summary>
        /// Gets the color for a program status.
        /// </summary>
        private Color GetStatusColor(ProgramStatus status)
        {
            return status switch
            {
                ProgramStatus.Running => Color.FromArgb(0, 255, 0), // Bright green
                ProgramStatus.Pending => Color.FromArgb(255, 255, 0), // Bright yellow
                ProgramStatus.CleaningUp => Color.Cyan, // BBS cycling during cleanup
                ProgramStatus.Stopped => Color.Gray,
                ProgramStatus.Crashed => Color.Red,
                _ => Color.White
            };
        }

        /// <summary>
        /// Right-click handler for program labels.
        /// </summary>
        private void ProgramLabel_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            if (sender is Label label && label.Tag is ManagedProgram program)
            {
                ShowProgramContextMenu(program, label, e.Location);
            }
        }

        /// <summary>
        /// Shows context menu for a program.
        /// </summary>
        private void ShowProgramContextMenu(ManagedProgram program, Control control, Point location)
        {
            var menu = new ContextMenuStrip();

            if (program.CanLaunch)
            {
                var launchItem = new ToolStripMenuItem("Launch Now");
                launchItem.Click += (s, e) => LaunchProgram(program);
                menu.Items.Add(launchItem);
            }

            if (program.CanCancelLaunch)
            {
                var cancelItem = new ToolStripMenuItem("Cancel Launch");
                cancelItem.Click += (s, e) => CancelProgramLaunch(program);
                menu.Items.Add(cancelItem);
            }

            if (program.CanStop)
            {
                var stopItem = new ToolStripMenuItem("Stop Application");
                stopItem.Click += (s, e) => StopProgram(program);
                menu.Items.Add(stopItem);
            }

            if (menu.Items.Count == 0)
            {
                var noActionsItem = new ToolStripMenuItem("No actions available");
                noActionsItem.Enabled = false;
                menu.Items.Add(noActionsItem);
            }

            menu.Show(control, location);
        }

        /// <summary>
        /// Launches a program immediately.
        /// </summary>
        private void LaunchProgram(ManagedProgram program)
        {
            try
            {
                string? workingDir = Path.GetDirectoryName(program.Path);
                var process = ProcessHelper.LaunchProgram(
                    program.Path,
                    workingDir,
                    string.IsNullOrWhiteSpace(program.Arguments) ? null : program.Arguments,
                    program.LaunchMinimized);

                if (process != null)
                {
                    program.Status = ProgramStatus.Running;
                    UpdateStatusLabel(program);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error launching {program.Name}:\n\n{ex.Message}",
                    "Launch Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Cancels a pending program launch.
        /// </summary>
        private void CancelProgramLaunch(ManagedProgram program)
        {
            if (!string.IsNullOrEmpty(program.AutoLaunchId))
            {
                _autoLaunchManager.CancelProgramLaunch(program.AutoLaunchId);
                program.Status = ProgramStatus.Stopped;
                program.SecondsRemaining = 0;
                UpdateStatusLabel(program);
                UpdateCancelButton();
            }
        }

        /// <summary>
        /// Stops a running program.
        /// </summary>
        private void StopProgram(ManagedProgram program)
        {
            var result = MessageBox.Show(
                $"Stop {program.Name}?\n\nThis will force-close the application.",
                "Confirm Stop",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                int killed = ProcessHelper.ForceKillProcess(program.ProcessName);
                if (killed > 0)
                {
                    program.Status = ProgramStatus.Stopped;
                    UpdateStatusLabel(program);
                }
            }
        }

        /// <summary>
        /// Updates the cancel button visibility.
        /// </summary>
        private void UpdateCancelButton()
        {
            bool hasPending = _programs.Any(p => p.Status == ProgramStatus.Pending);

            if (hasPending && _cancelButton == null)
            {
                float sf = Math.Max(1f, this.DeviceDpi / 96f);
                // Create cancel button inside the scrollable panel so it appears below program rows
                _cancelButton = new Button
                {
                    Text = "Cancel Auto-Launches",
                    Location = new Point((int)(30 * sf), _lastContentHeight + 4),
                    Size = new Size((int)(200 * sf), (int)(25 * sf)),
                    BackColor = Color.FromArgb(64, 64, 128),
                    ForeColor = Color.White,
                    Font = _cancelButtonFont,
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand
                };
                _cancelButton.FlatAppearance.BorderColor = Color.Cyan;
                _cancelButton.Click += CancelButton_Click;
                _programListPanel.Controls.Add(_cancelButton);
            }
            else if (!hasPending && _cancelButton != null)
            {
                _programListPanel.Controls.Remove(_cancelButton);
                _cancelButton.Dispose();
                _cancelButton = null;
            }
        }

        /// <summary>
        /// Cancel button click handler.
        /// </summary>
        private void CancelButton_Click(object? sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Cancel all pending auto-launches?",
                "Confirm Cancel",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                _autoLaunchManager.StopAllLaunches();
            }
        }

        /// <summary>
        /// Handles countdown tick events from AutoLaunchManager.
        /// </summary>
        private void OnCountdownTick(object? sender, AutoLaunchCountdownEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => OnCountdownTick(sender, e)));
                return;
            }

            var program = _programs.FirstOrDefault(p => p.AutoLaunchId == e.ProgramId);
            if (program != null)
            {
                program.Status = ProgramStatus.Pending;
                program.SecondsRemaining = e.SecondsRemaining;
                UpdateStatusLabel(program);
            }
        }

        /// <summary>
        /// Handles program launched events from AutoLaunchManager.
        /// </summary>
        private void OnProgramLaunched(object? sender, AutoLaunchEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => OnProgramLaunched(sender, e)));
                return;
            }

            var program = _programs.FirstOrDefault(p => p.AutoLaunchId == e.ProgramId);
            if (program != null)
            {
                program.Status = e.Success ? ProgramStatus.Running : ProgramStatus.Stopped;
                program.SecondsRemaining = 0;
                UpdateStatusLabel(program);
                UpdateCancelButton();
            }

            // Check if all launches complete and auto-hide enabled
            if (_autoHide && !_programs.Any(p => p.Status == ProgramStatus.Pending))
            {
                this.Hide();
            }
        }

        /// <summary>
        /// Handles all launches cancelled event.
        /// </summary>
        private void OnAllLaunchesCancelled(object? sender, EventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => OnAllLaunchesCancelled(sender, e)));
                return;
            }

            foreach (var program in _programs.Where(p => p.Status == ProgramStatus.Pending))
            {
                program.Status = ProgramStatus.Stopped;
                program.SecondsRemaining = 0;
                UpdateStatusLabel(program);
            }

            UpdateCancelButton();
        }

        /// <summary>
        /// Updates a single program's status label.
        /// </summary>
        private void UpdateStatusLabel(ManagedProgram program)
        {
            if (_statusLabels.TryGetValue(program.Name, out var label))
            {
                label.Text = program.GetStatusText();
                label.ForeColor = GetStatusColor(program.Status);
            }
        }

        /// <summary>
        /// Timer tick - check running status of all programs.
        /// </summary>
        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            foreach (var program in _programs)
            {
                // Skip programs with pending countdown
                if (program.Status == ProgramStatus.Pending)
                    continue;

                // The BBS is handled by _bbsTimer (500ms) so the launcher restores quickly after a
                // shutdown. Skip it here — checking it again at 2s would only duplicate the work.
                if (program.IsBBS)
                    continue;

                bool isRunning = ProcessHelper.IsProcessRunning(program.ProcessName);

                // Detect stopped (was running, now stopped — could be clean shutdown or crash)
                if (program.Status == ProgramStatus.Running && !isRunning)
                {
                    program.Status = ProgramStatus.Stopped;
                    UpdateStatusLabel(program);
                }
                // Update running status
                else if (program.Status != ProgramStatus.Running && isRunning)
                {
                    program.Status = ProgramStatus.Running;
                    UpdateStatusLabel(program);
                }
                else if (program.Status != ProgramStatus.Pending && !isRunning)
                {
                    program.Status = ProgramStatus.Stopped;
                    UpdateStatusLabel(program);
                }
            }
        }

        /// <summary>
        /// Fast timer tick (500ms) — BBS row only.
        ///
        /// This is deliberately separate from the 2s program-list timer: the BBS is the one row whose
        /// state change moves the launcher window, so it is sampled four times as often while nothing
        /// else pays for the extra process enumeration.
        /// </summary>
        private void BbsTimer_Tick(object? sender, EventArgs e)
        {
            foreach (var program in _programs)
            {
                if (!program.IsBBS || program.Status == ProgramStatus.Pending)
                    continue;

                UpdateBBSStatus(program, serverRunning: ProcessHelper.IsProcessRunning(program.ProcessName));
                return; // only ever one BBS row
            }
        }

        /// <summary>
        /// Cleanup-aware BBS status transition.
        ///
        /// The BBS has two processes: wgserver.exe (the server) and wgsappgo.exe (the "Go!"
        /// supervisor). During a cleanup the server exits and restarts — sometimes for several
        /// minutes on slower hardware with more modules — while the supervisor stays alive. The
        /// supervisor only exits on a genuine shutdown. So:
        ///
        ///   - wgserver up                                        -> Running
        ///   - wgserver down, supervisor up                       -> CleaningUp (hold; do NOT restore)
        ///   - wgserver down, supervisor down, TICKLER.RUN present -> Crashed
        ///   - wgserver down, supervisor down, TICKLER.RUN absent  -> Stopped (real shutdown; restore now)
        ///
        /// The supervisor is checked BEFORE TICKLER.RUN so that a cleanup — where wgserver exits a moment
        /// before the TICKLER module deletes TICKLER.RUN, while wgsappgo keeps running — is never misread
        /// as a crash. Startup (supervisor up before the server) is excluded by only transitioning to
        /// CleaningUp / Crashed / Stopped from a Running or CleaningUp state.
        /// </summary>
        private void UpdateBBSStatus(ManagedProgram program, bool serverRunning)
        {
            var prev = program.Status;
            bool wasUp = prev == ProgramStatus.Running || prev == ProgramStatus.CleaningUp || prev == ProgramStatus.Crashed;

            if (serverRunning)
            {
                // Server is up — fully running. Any down-streak is over.
                _bbsDownClock.Reset();
                if (prev != ProgramStatus.Running)
                {
                    program.Status = ProgramStatus.Running;
                    UpdateStatusLabel(program);
                }
                return;
            }

            // Server down. Check the wgsappgo supervisor FIRST: while it is alive the BBS is not truly
            // down — a cleanup/restart cycle or self-recovery is in progress — so this can never be a
            // crash we act on, no matter what TICKLER.RUN shows. At the very start of a cleanup wgserver
            // exits a moment before the TICKLER module deletes TICKLER.RUN, so the file can still be
            // present here; without this guard that timing gap flips the BBS to Crashed and (with
            // monitoring disabled) pops the launcher up mid-cleanup. This mirrors the RestartManager
            // invariant: a crash requires wgserver down AND wgsappgo gone AND TICKLER.RUN present.
            bool supervisorRunning = ProcessHelper.IsProcessRunning(BBS_SUPERVISOR_PROCESS);

            // Note: "wasUp" deliberately excludes Stopped so that a normal startup — where wgsappgo comes
            // up before wgserver — is not displayed as a cleanup. That also means a retracted false stop
            // (see MainForm's retract watch) leaves the row reading Stopped until wgserver returns and
            // flips it to Running. Cosmetic only, and self-correcting; not worth breaking startup for.
            if (supervisorRunning && wasUp)
            {
                // Server down but the supervisor is alive: cleanup / restart cycle. Hold.
                _bbsDownClock.Reset();
                if (prev != ProgramStatus.CleaningUp)
                {
                    program.Status = ProgramStatus.CleaningUp;
                    UpdateStatusLabel(program);
                }
                return;
            }

            // Both wgserver AND wgsappgo look gone this sample. That can still be a transient cleanup
            // window — on some setups the cleanup exits the Go! supervisor and a batch loop relaunches
            // it, so both processes are briefly absent. Do NOT restore on the first sample: both must
            // stay gone for _bbsDownConfirmMs of wall-clock time before we decide it is real. Any
            // reappearance of either process (handled by the early returns above) resets the clock.
            if (wasUp)
            {
                if (!_bbsDownClock.IsRunning)
                    _bbsDownClock.Restart();

                if (_bbsDownClock.ElapsedMilliseconds < _bbsDownConfirmMs)
                {
                    // Hold the display at CleaningUp while we debounce — this is exactly the window a
                    // mid-cleanup supervisor bounce lives in, and it should not read as Crashed/Stopped yet.
                    if (prev != ProgramStatus.CleaningUp && prev != ProgramStatus.Crashed)
                    {
                        program.Status = ProgramStatus.CleaningUp;
                        UpdateStatusLabel(program);
                    }
                    return;
                }
            }

            // Supervisor is gone. The TICKLER module deletes TICKLER.RUN on a clean shutdown/cleanup, so
            // a TICKLER.RUN still present while both processes are down means the BBS CRASHED.
            string bbsPath = _config.GetValue("Paths", "BBSPath", "");
            bool ticklerPresent = TicklerMonitor.Exists(bbsPath);

            if (ticklerPresent && wasUp)
            {
                bool firstDetection = prev != ProgramStatus.Crashed;
                if (firstDetection)
                {
                    program.Status = ProgramStatus.Crashed;
                    UpdateStatusLabel(program);
                }

                // If BBS Monitoring is enabled, the RestartManager owns recovery and the restore —
                // do NOT pop the launcher up here. If it's disabled, surface the crash by restoring
                // the launcher once so the sysop sees it.
                bool monitoringEnabled = _config.GetBool("Monitoring", "Enabled", false);
                if (firstDetection)
                    LogBbsDownDecision("Crashed", serverRunning, supervisorRunning, ticklerPresent,
                        bbsPath, restored: !monitoringEnabled,
                        note: monitoringEnabled ? "monitoring enabled — RestartManager owns restore" : "monitoring disabled — restoring launcher");
                if (firstDetection && !monitoringEnabled)
                    OnBBSStopped();

                return;
            }

            // Server down, supervisor gone, TICKLER gone (or not found at BBSPath) = genuine shutdown.
            if (wasUp)
            {
                LogBbsDownDecision("Stopped", serverRunning, supervisorRunning, ticklerPresent,
                    bbsPath, restored: true, note: "both processes gone, no TICKLER.RUN — treated as shutdown");
                program.Status = ProgramStatus.Stopped;
                UpdateStatusLabel(program);
                OnBBSStopped();
            }
            else if (prev != ProgramStatus.Stopped)
            {
                program.Status = ProgramStatus.Stopped;
                UpdateStatusLabel(program);
            }
        }

        /// <summary>
        /// Records the exact signal state at the moment the App Manager decides the BBS is down and
        /// restores the launcher. This path is otherwise silent, so when a sysop reports a spurious
        /// restore during a cleanup, audit.log shows precisely which signal drove it — most importantly
        /// whether BBSPath actually resolves to the folder where SNTICKLR writes TICKLER.RUN.
        /// </summary>
        private void LogBbsDownDecision(string decision, bool serverRunning, bool supervisorRunning,
            bool ticklerPresent, string bbsPath, bool restored, string note)
        {
            string ticklerFullPath = TicklerMonitor.GetTicklerPath(bbsPath) ?? "(no BBSPath configured)";
            Program.LogInfo("AppManager.BBSDown",
                $"decision={decision} restoredLauncher={restored} " +
                $"bothDownForMs={_bbsDownClock.ElapsedMilliseconds} confirmMs={_bbsDownConfirmMs} " +
                $"wgserver={(serverRunning ? "up" : "down")} wgsappgo={(supervisorRunning ? "up" : "down")} " +
                $"ticklerPresent={ticklerPresent} ticklerPath=\"{ticklerFullPath}\" | {note}");
        }

        /// <summary>
        /// Called when the BBS has genuinely stopped (server and supervisor both gone).
        /// Restores the main launcher window immediately.
        /// </summary>
        private void OnBBSStopped()
        {
            // Fire event to restore main launcher window
            BBSCrashed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Event fired when the BBS has fully stopped (real shutdown, not a cleanup cycle).
        /// </summary>
        public event EventHandler? BBSCrashed;

        /// <summary>
        /// Shows the App Manager and brings to front.
        /// </summary>
        public new void Show()
        {
            base.Show();
            this.BringToFront();
            // Timers run continuously (never stopped on hide) so only start them if somehow not running.
            if (!_updateTimer.Enabled) _updateTimer.Start();
            if (!_bbsTimer.Enabled) _bbsTimer.Start();
            InitializePrograms();
            UpdateDisplay();
        }

        /// <summary>
        /// Enables bottom-edge resize on this borderless form.
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_NCHITTEST)
            {
                var cursor = this.PointToClient(Cursor.Position);
                if (cursor.Y >= this.ClientSize.Height - 6)
                {
                    m.Result = (IntPtr)HTBOTTOM;
                }
            }
        }

        /// <summary>
        /// Form closing - save settings and clean up resources.
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Don't actually close on user X click, just hide
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                SaveSettings();
                return;
            }

            // Application is actually shutting down — clean up
            _updateTimer.Stop();
            _bbsTimer.Stop();
            _boldFont.Dispose();
            _regularFont.Dispose();
            _cancelButtonFont.Dispose();

            SaveSettings();
            base.OnFormClosing(e);
        }

        /// <summary>
        /// Make title bar draggable.
        /// </summary>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && e.Y < 30) // Title bar area
            {
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }
    }
}
