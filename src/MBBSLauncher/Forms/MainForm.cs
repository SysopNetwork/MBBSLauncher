// MBBSLauncher - Main Form
// Created by Mark Laudenbach with Love in Iowa
// https://github.com/SysopNetwork/MBBSLauncher
//
// File: Forms/MainForm.cs
// Version: v1.85
//
// Change History:
// 26.01.07.1 - 06:00PM - Initial creation
// 26.01.07.3 - 07:15PM - Added better error handling for startup
// 26.01.12.1 - Added system tray icon with context menu and status tracking
// 26.01.12.2 - Added mouse navigation with hover effects and cursor changes
// 26.01.12.3 - Added auto-start BBS on startup with countdown and cancel
// 26.01.12.4 - Added F1 Help dialog and F2 Module Editor launcher
// 26.01.23.1 - Added Ghost3 auto-launch support with countdown
// 26.02.07.1 - v1.5 - Added AutoLaunchManager integration for multi-program auto-launch
// 26.02.11.1 - v1.6 - Administrator privileges now required via app.manifest
// 26.02.19.1 - v1.60 - Neutral wording for BBS stop tray notification
// 26.02.19.2 - v1.70 - Fix paint error on close: Bitmap copy prevents stream-disposal crash
// 26.02.19.3 - v1.70 - Red heart painted in Windows title bar via WM_NCPAINT
// 26.06.04.1 - v1.80 - Removed double-restore: Task.Run watcher no longer calls RestoreFromTray
//                      (AppManager BBSCrashed event is the single path for restoring the launcher)
// 26.06.04.2 - v1.80 - Added CancellationToken to wgserver watcher Task.Run so it exits cleanly
//                      when the form is closed while monitoring is in progress
// 26.06.04.3 - v1.80 - Fixed Ghost3/auto-launch countdown overlap: both used the same bottom Y
//                      position and painted over each other. Ghost3 now stacks above auto-launch rows.
// 26.06.04.4 - v1.85 - Fixed Ghost3/AutoStart overlap: DrawGhost3Countdown now returns its pixel
//                      height so DrawAutoStartCountdown stacks correctly above it
// 26.06.04.5 - v1.85 - LaunchModulesEditor Task.Run now uses _launchMonitorCts and IsDisposed
//                      guard to prevent ObjectDisposedException when form closes while editor runs
// 26.06.04.6 - v1.85 - Removed dead LaunchURL() method (no callers since URL zones removed in v1.80)

using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MBBSLauncher.Forms
{
    public partial class MainForm : Form
    {
        private ConfigManager _config;
        private Image? _backgroundImage;
        private System.Windows.Forms.Timer? _digitTimer;
        private string _digitBuffer = "";

        // System tray components
        private NotifyIcon? _trayIcon;
        private ContextMenuStrip? _trayMenu;
        private ToolStripMenuItem? _showMenuItem;
        private ToolStripMenuItem? _startBBSMenuItem;
        private ToolStripMenuItem? _bringToFrontMenuItem;
        private ToolStripMenuItem? _appManagerMenuItem;
        private ToolStripMenuItem? _configMenuItem;
        private ToolStripMenuItem? _exitMenuItem;

        // Running program state tracking
        private string? _runningProgramName;
        private System.Diagnostics.Process? _runningProcess;
        private bool _isFirstMinimizeToTray = true;


        // Auto-start BBS countdown
        private System.Windows.Forms.Timer? _autoStartTimer;
        private int _autoStartCountdown = 0;
        private bool _autoStartCancelled = false;

        // Ghost3 countdown (v1.20 legacy - kept for compatibility)
        private System.Windows.Forms.Timer? _ghost3Timer;
        private int _ghost3Countdown = 0;
        private bool _ghost3Cancelled = false;

        // Auto-launch programs (v1.5 feature)
        private Core.AutoLaunchManager? _autoLaunchManager;
        private System.Collections.Generic.Dictionary<string, int> _autoLaunchCountdowns = new System.Collections.Generic.Dictionary<string, int>();

        // App Manager
        private AppManagerForm? _appManagerForm;

        // Track window state for restore detection
        private FormWindowState _previousWindowState = FormWindowState.Normal;

        // Cancellation for the background wgserver watcher task
        private System.Threading.CancellationTokenSource? _launchMonitorCts;

        public MainForm()
        {
            try
            {
                InitializeComponent();
                _config = new ConfigManager();
                InitializeCustomComponents();

                // Check if wgserver is already running on startup
                try
                {
                    if (ProcessHelper.IsWGServerRunning())
                    {
                        var result = MessageBox.Show(
                            "WGServer is already running!\n\nWould you like to bring it to the foreground?",
                            "MBBSLauncher",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Information);

                        if (result == DialogResult.Yes)
                        {
                            var process = ProcessHelper.GetProcess("wgserver");
                            if (process != null)
                            {
                                ProcessHelper.BringToForeground(process);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log but don't fail - process detection is optional
                    Program.LogError("WGServer detection", ex);
                }

                // Search for BBS folders on first run
                try
                {
                    if (string.IsNullOrEmpty(_config.GetValue("Paths", "BBSPath")) ||
                        !Directory.Exists(_config.GetValue("Paths", "BBSPath")))
                    {
                        _config.SearchForBBSFolders();
                        _config.SaveConfig();
                    }
                }
                catch (Exception ex)
                {
                    // Log but don't fail - folder search is optional
                    Program.LogError("BBS folder search", ex);
                }

                LoadBackgroundImage();
                LoadWindowSettings();
            }
            catch (Exception ex)
            {
                Program.LogError("MainForm constructor", ex);
                throw; // Re-throw to be caught by global handler
            }
        }

        private void InitializeCustomComponents()
        {
            this.Text = $"{Program.APP_NAME} {Program.APP_VERSION}. Created with Love \u2764 by {Program.AUTHOR} in Iowa, USA.";
            this.Size = new Size(960, 540); // 16:9 aspect ratio
            this.MinimumSize = new Size(640, 360); // Minimum 16:9
            this.StartPosition = FormStartPosition.CenterScreen;
            this.KeyPreview = true;
            this.BackColor = Color.FromArgb(0, 0, 170); // Classic DOS blue
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.ResizeRedraw, true); // Auto-invalidate on resize

            // Load application icon
            LoadApplicationIcon();

            // Initialize digit input timer
            _digitTimer = new System.Windows.Forms.Timer();
            _digitTimer.Interval = 1000; // 1 second timeout for multi-digit input
            _digitTimer.Tick += DigitTimer_Tick;

            // Initialize auto-start countdown timer
            _autoStartTimer = new System.Windows.Forms.Timer();
            _autoStartTimer.Interval = 1000; // 1 second interval
            _autoStartTimer.Tick += AutoStartTimer_Tick;

            // Initialize Ghost3 countdown timer
            _ghost3Timer = new System.Windows.Forms.Timer();
            _ghost3Timer.Interval = 1000; // 1 second interval
            _ghost3Timer.Tick += Ghost3Timer_Tick;

            // Initialize Auto-Launch Manager (v1.5)
            _autoLaunchManager = new Core.AutoLaunchManager();
            _autoLaunchManager.LoadFromConfig(_config);
            _autoLaunchManager.CountdownTick += AutoLaunchManager_CountdownTick;
            _autoLaunchManager.ProgramLaunched += AutoLaunchManager_ProgramLaunched;
            _autoLaunchManager.AllLaunchesCancelled += AutoLaunchManager_AllLaunchesCancelled;

            // Initialize App Manager
            _appManagerForm = new AppManagerForm(_autoLaunchManager, _config);
            _appManagerForm.BBSCrashed += AppManager_BBSCrashed;

            // Handle keyboard input
            this.KeyDown += MainForm_KeyDown;
            this.Paint += MainForm_Paint;
            this.Resize += MainForm_Resize;
            this.FormClosing += MainForm_FormClosing;
            this.Move += MainForm_Move;
            this.MouseClick += MainForm_MouseClick;
            this.MouseMove += MainForm_MouseMove;
            this.MouseLeave += MainForm_MouseLeave;
            this.VisibleChanged += MainForm_VisibleChanged;
            this.Activated += MainForm_Activated;
            this.Shown += MainForm_Shown;

            // Initialize system tray icon
            InitializeTrayIcon();
        }

        private void InitializeTrayIcon()
        {
            // Create context menu for tray icon
            _trayMenu = new ContextMenuStrip();

            _showMenuItem = new ToolStripMenuItem("Show Launcher", null, TrayMenu_ShowLauncher);
            _showMenuItem.Font = new Font(_showMenuItem.Font, FontStyle.Bold);

            _startBBSMenuItem = new ToolStripMenuItem("Start BBS (Go!)", null, TrayMenu_StartBBS);

            _bringToFrontMenuItem = new ToolStripMenuItem("Bring Program to Front", null, TrayMenu_BringToFront);
            _bringToFrontMenuItem.Visible = false; // Hidden by default, shown when program is running

            _appManagerMenuItem = new ToolStripMenuItem("App Manager", null, TrayMenu_ShowAppManager);

            _configMenuItem = new ToolStripMenuItem("Configuration (F12)", null, TrayMenu_OpenConfig);

            _exitMenuItem = new ToolStripMenuItem("Exit", null, TrayMenu_Exit);

            _trayMenu.Items.Add(_showMenuItem);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(_startBBSMenuItem);
            _trayMenu.Items.Add(_bringToFrontMenuItem);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(_appManagerMenuItem);
            _trayMenu.Items.Add(_configMenuItem);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(_exitMenuItem);

            // Create tray icon
            _trayIcon = new NotifyIcon();
            _trayIcon.Text = "MBBSLauncher - Idle";
            _trayIcon.ContextMenuStrip = _trayMenu;
            _trayIcon.DoubleClick += TrayIcon_DoubleClick;

            // Use form icon for tray icon
            if (this.Icon != null)
            {
                _trayIcon.Icon = this.Icon;
            }

            // Show tray icon based on settings
            bool showTrayIcon = _config.GetValue("Settings", "ShowTrayIcon", "true").ToLower() == "true";
            _trayIcon.Visible = showTrayIcon;
        }

        private void TrayIcon_DoubleClick(object? sender, EventArgs e)
        {
            RestoreFromTray();
        }

        private void TrayMenu_ShowLauncher(object? sender, EventArgs e)
        {
            RestoreFromTray();
        }

        private void TrayMenu_StartBBS(object? sender, EventArgs e)
        {
            // Only start if BBS is not already running
            if (!ProcessHelper.IsWGServerRunning())
            {
                RestoreFromTray();
                LaunchOption(5); // Option 5 is "Go!"
            }
            else
            {
                MessageBox.Show(
                    "WGServer is already running!",
                    "MBBSLauncher",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private void TrayMenu_BringToFront(object? sender, EventArgs e)
        {
            System.Diagnostics.Process? target = null;

            // For the BBS, always look up wgserver fresh — the tracked _runningProcess
            // may be the wgsappgo launcher which has already exited, or its handle may be stale.
            if (ProcessHelper.IsWGServerRunning())
            {
                target = ProcessHelper.GetProcess("wgserver");
            }

            // For non-BBS programs (or if wgserver wasn't found), use the tracked process.
            if (target == null && _runningProcess != null)
            {
                try
                {
                    _runningProcess.Refresh(); // ensure window handle is current
                    if (!_runningProcess.HasExited)
                        target = _runningProcess;
                }
                catch { }
            }

            if (target != null)
            {
                target.Refresh(); // always refresh before reading MainWindowHandle
                ProcessHelper.BringToForeground(target);
            }
        }

        private void TrayMenu_ShowAppManager(object? sender, EventArgs e)
        {
            if (_appManagerForm != null)
            {
                if (_appManagerForm.Visible)
                {
                    _appManagerForm.BringToFront();
                }
                else
                {
                    _appManagerForm.Show();
                }
            }
        }

        private void TrayMenu_OpenConfig(object? sender, EventArgs e)
        {
            RestoreFromTray();
            OpenConfigEditor();
        }

        private void TrayMenu_Exit(object? sender, EventArgs e)
        {
            // Close the application
            Application.Exit();
        }

        private void RestoreFromTray()
        {
            this.Show();
            this.ShowInTaskbar = true;
            this.WindowState = FormWindowState.Normal;
            this.Activate();
            this.BringToFront();
            this.Invalidate();
            this.Refresh();
        }

        private void MinimizeToTray()
        {
            bool minimizeToTray = _config.GetValue("Settings", "MinimizeToTray", "true").ToLower() == "true";

            if (minimizeToTray && _trayIcon != null && _trayIcon.Visible)
            {
                this.WindowState = FormWindowState.Minimized;

                // Show balloon tip on first minimize
                if (_isFirstMinimizeToTray)
                {
                    _trayIcon.ShowBalloonTip(
                        2000,
                        "MBBSLauncher",
                        "MBBSLauncher is still running in the system tray.",
                        ToolTipIcon.Info);
                    _isFirstMinimizeToTray = false;
                }
            }
        }

        private void UpdateTrayStatus(string? programName, System.Diagnostics.Process? process)
        {
            _runningProgramName = programName;
            _runningProcess = process;

            if (_trayIcon == null) return;

            if (!string.IsNullOrEmpty(programName))
            {
                // Program is running
                _trayIcon.Text = $"MBBSLauncher - Running: {programName}";

                if (_bringToFrontMenuItem != null)
                {
                    _bringToFrontMenuItem.Text = $"Bring {programName} to Front";
                    _bringToFrontMenuItem.Visible = true;
                }

                if (_startBBSMenuItem != null)
                {
                    _startBBSMenuItem.Enabled = false;
                }
            }
            else
            {
                // Idle state
                _trayIcon.Text = "MBBSLauncher - Idle";

                if (_bringToFrontMenuItem != null)
                {
                    _bringToFrontMenuItem.Visible = false;
                }

                if (_startBBSMenuItem != null)
                {
                    _startBBSMenuItem.Enabled = true;
                }
            }
        }

        private void LoadBackgroundImage()
        {
            try
            {
                // Try to load from embedded resources first
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceName = "MBBSLauncher.Resources.background.png";

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        // Use Bitmap copy so the image doesn't hold a reference to the
                        // now-disposed stream — prevents ArgumentException in the Paint
                        // handler when GDI+ accesses image metadata after close.
                        using (var temp = Image.FromStream(stream))
                        {
                            _backgroundImage = new Bitmap(temp);
                        }
                        return;
                    }
                }

                // Fallback to file system if embedded resource not found
                string imagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "background.png");
                if (File.Exists(imagePath))
                {
                    _backgroundImage = Image.FromFile(imagePath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading background image: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void LoadApplicationIcon()
        {
            try
            {
                // Try to load icon from file system first
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "icon.ico");
                if (File.Exists(iconPath))
                {
                    this.Icon = new Icon(iconPath);
                    return;
                }

                // Fallback: try to extract from embedded resources
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceName = "MBBSLauncher.Resources.icon.ico";

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        this.Icon = new Icon(stream);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail - icon is optional
                Program.LogError("LoadApplicationIcon", ex);
            }
        }

        private void LoadWindowSettings()
        {
            try
            {
                // Load window size
                string widthStr = _config.GetValue("Window", "Width");
                string heightStr = _config.GetValue("Window", "Height");

                if (int.TryParse(widthStr, out int width) && int.TryParse(heightStr, out int height))
                {
                    if (width >= this.MinimumSize.Width && height >= this.MinimumSize.Height)
                    {
                        this.Size = new Size(width, height);
                    }
                }

                // Load window position
                string xStr = _config.GetValue("Window", "X");
                string yStr = _config.GetValue("Window", "Y");

                if (int.TryParse(xStr, out int x) && int.TryParse(yStr, out int y))
                {
                    // Ensure the window is visible on screen
                    Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
                    if (x >= workingArea.Left && x < workingArea.Right - 100 &&
                        y >= workingArea.Top && y < workingArea.Bottom - 100)
                    {
                        this.StartPosition = FormStartPosition.Manual;
                        this.Location = new Point(x, y);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail - window settings are optional
                Program.LogError("LoadWindowSettings", ex);
            }
        }

        private void SaveWindowSettings()
        {
            try
            {
                if (this.WindowState == FormWindowState.Normal)
                {
                    _config.SetValue("Window", "X", this.Location.X.ToString());
                    _config.SetValue("Window", "Y", this.Location.Y.ToString());
                    _config.SetValue("Window", "Width", this.Size.Width.ToString());
                    _config.SetValue("Window", "Height", this.Size.Height.ToString());
                    _config.SaveConfig();
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail - window settings are optional
                Program.LogError("SaveWindowSettings", ex);
            }
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            // Cancel any background process-watcher task so it doesn't Invoke on a disposed form
            _launchMonitorCts?.Cancel();
            SaveWindowSettings();
        }

        private void MainForm_Move(object? sender, EventArgs e)
        {
            // Save position when moved (debounced by only saving on close)
        }

        /// <summary>
        /// Handles BBS crash event from App Manager.
        /// Restores the main launcher window to make it visible to the user.
        /// </summary>
        private void AppManager_BBSCrashed(object? sender, EventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => AppManager_BBSCrashed(sender, e)));
                return;
            }

            // Restore main window from tray
            RestoreFromTray();

            // Show notification
            if (_trayIcon != null)
            {
                _trayIcon.ShowBalloonTip(
                    5000,
                    "BBS has stopped.",
                    "The Major BBS has stopped. Main window restored.",
                    ToolTipIcon.Warning);
            }
        }

        private void MainForm_VisibleChanged(object? sender, EventArgs e)
        {
            // Force repaint when visibility changes
            if (this.Visible)
            {
                this.Invalidate();
                this.Refresh();
            }
        }

        private void MainForm_Activated(object? sender, EventArgs e)
        {
            // Force repaint when window is activated
            this.Invalidate();
        }

        private void MainForm_Shown(object? sender, EventArgs e)
        {
            // Force repaint when window is first shown
            this.Invalidate();
            this.Refresh();

            // Check if auto-start BBS is enabled
            CheckAutoStartBBS();
        }

        private void CheckAutoStartBBS()
        {
            // Check if auto-start is enabled
            bool autoStartEnabled = _config.GetValue("Settings", "AutoStartBBS", "false").ToLower() == "true";
            if (!autoStartEnabled) return;

            // Don't auto-start if BBS is already running
            if (ProcessHelper.IsWGServerRunning()) return;

            // Get delay from config
            if (!int.TryParse(_config.GetValue("Settings", "AutoStartDelay", "5"), out int delay))
            {
                delay = 5;
            }

            // Clamp delay to reasonable range
            delay = Math.Max(0, Math.Min(60, delay));

            // Start countdown
            _autoStartCountdown = delay;
            _autoStartCancelled = false;

            if (delay == 0)
            {
                // Immediate start
                LaunchBBSAfterCountdown();
            }
            else
            {
                // Start timer
                _autoStartTimer?.Start();
                this.Invalidate(); // Show countdown message
            }
        }

        private void AutoStartTimer_Tick(object? sender, EventArgs e)
        {
            if (_autoStartCancelled)
            {
                _autoStartTimer?.Stop();
                _autoStartCountdown = 0;
                this.Invalidate();
                return;
            }

            _autoStartCountdown--;

            if (_autoStartCountdown <= 0)
            {
                _autoStartTimer?.Stop();
                LaunchBBSAfterCountdown();
            }
            else
            {
                this.Invalidate(); // Update countdown display
            }
        }

        private void LaunchBBSAfterCountdown()
        {
            _autoStartCountdown = 0;
            this.Invalidate();

            // Launch Option 5 (Go!)
            LaunchOption(5);

            // Check if quiet mode is enabled
            bool quietMode = _config.GetValue("Settings", "QuietMode", "false").ToLower() == "true";
            if (quietMode)
            {
                MinimizeToTray();
            }
        }

        private void CancelAutoStart()
        {
            if (_autoStartCountdown > 0)
            {
                _autoStartCancelled = true;
                _autoStartTimer?.Stop();
                _autoStartCountdown = 0;
                this.Invalidate();
            }
        }

        private void Ghost3Timer_Tick(object? sender, EventArgs e)
        {
            if (_ghost3Cancelled)
            {
                _ghost3Timer?.Stop();
                _ghost3Countdown = 0;
                this.Invalidate();
                return;
            }

            _ghost3Countdown--;

            if (_ghost3Countdown <= 0)
            {
                _ghost3Timer?.Stop();
                LaunchGhost3();
            }
            else
            {
                this.Invalidate(); // Update countdown display
            }
        }

        private void StartGhost3Countdown()
        {
            // Check if Ghost3 is enabled
            bool ghost3Enabled = _config.GetValue("Settings", "Ghost3Enabled", "false").ToLower() == "true";
            if (!ghost3Enabled) return;

            // Get delay from config
            if (!int.TryParse(_config.GetValue("Settings", "Ghost3Delay", "60"), out int delay))
            {
                delay = 60;
            }

            // Clamp delay to reasonable range
            delay = Math.Max(0, Math.Min(300, delay));

            // Start countdown
            _ghost3Countdown = delay;
            _ghost3Cancelled = false;

            if (delay == 0)
            {
                // Immediate launch
                LaunchGhost3();
            }
            else
            {
                // Start timer
                _ghost3Timer?.Start();
                this.Invalidate(); // Show countdown message
            }
        }

        private void LaunchGhost3()
        {
            _ghost3Countdown = 0;
            this.Invalidate();

            string ghost3Path = _config.GetValue("Settings", "Ghost3Path", @"C:\Ghost3\Ghost3.exe");

            if (string.IsNullOrWhiteSpace(ghost3Path))
            {
                return; // Silently skip if not configured
            }

            if (!File.Exists(ghost3Path))
            {
                MessageBox.Show(
                    $"Ghost3 not found:\n{ghost3Path}\n\nPress F12 to update the Ghost3 path in configuration.",
                    "Ghost3 Not Found",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // Launch Ghost3 without monitoring
            string? workingDir = Path.GetDirectoryName(ghost3Path);
            ProcessHelper.LaunchProgram(ghost3Path, workingDir, null);
        }

        private void CancelGhost3()
        {
            if (_ghost3Countdown > 0)
            {
                _ghost3Cancelled = true;
                _ghost3Timer?.Stop();
                _ghost3Countdown = 0;
                this.Invalidate();
            }
        }

        /// <summary>
        /// Event handler for auto-launch countdown ticks - updates UI.
        /// </summary>
        private void AutoLaunchManager_CountdownTick(object? sender, Core.AutoLaunchCountdownEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    _autoLaunchCountdowns[e.ProgramId] = e.SecondsRemaining;
                    this.Invalidate();
                });
            }
            else
            {
                _autoLaunchCountdowns[e.ProgramId] = e.SecondsRemaining;
                this.Invalidate();
            }
        }

        /// <summary>
        /// Event handler for program launches - removes from countdown UI.
        /// </summary>
        private void AutoLaunchManager_ProgramLaunched(object? sender, Core.AutoLaunchEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    _autoLaunchCountdowns.Remove(e.ProgramId);
                    this.Invalidate();
                });
            }
            else
            {
                _autoLaunchCountdowns.Remove(e.ProgramId);
                this.Invalidate();
            }
        }

        /// <summary>
        /// Event handler for launch cancellation - clears countdown UI.
        /// </summary>
        private void AutoLaunchManager_AllLaunchesCancelled(object? sender, EventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    _autoLaunchCountdowns.Clear();
                    this.Invalidate();
                });
            }
            else
            {
                _autoLaunchCountdowns.Clear();
                this.Invalidate();
            }
        }

        private void CancelAllAutoLaunches()
        {
            if (_autoLaunchCountdowns.Count > 0)
            {
                _autoLaunchManager?.StopAllLaunches();
                _autoLaunchCountdowns.Clear();
                this.Invalidate();
            }
        }

        private void MainForm_Paint(object? sender, PaintEventArgs e)
        {
            if (_backgroundImage != null)
            {
                // Draw background image scaled to fit window while maintaining aspect ratio
                e.Graphics.DrawImage(_backgroundImage, 0, 0, this.ClientSize.Width, this.ClientSize.Height);
            }

            // Stack countdowns from the bottom up so they never overlap.
            // Each method returns the total pixel height it consumed; the next caller
            // passes that as its bottomOffset so it draws above the previous block.
            int bottomOffset = 0;

            if (_autoLaunchCountdowns.Count > 0)
                bottomOffset = DrawAutoLaunchCountdowns(e.Graphics);

            if (_ghost3Countdown > 0)
                bottomOffset += DrawGhost3Countdown(e.Graphics, bottomOffset);

            if (_autoStartCountdown > 0)
                DrawAutoStartCountdown(e.Graphics, bottomOffset);
        }

        /// <summary>
        /// Draws the auto-start countdown message above any existing bottom-anchored banners.
        /// bottomOffset is the total pixel height already consumed by lower banners.
        /// </summary>
        private void DrawAutoStartCountdown(Graphics g, int bottomOffset = 0)
        {
            string message = $"Auto-starting BBS in {_autoStartCountdown} second{(_autoStartCountdown != 1 ? "s" : "")}... Press any key or click to cancel";

            using (Font font = new Font("Consolas", 12f, FontStyle.Bold))
            {
                SizeF textSize = g.MeasureString(message, font);

                float x = (this.ClientSize.Width - textSize.Width) / 2;
                float y = this.ClientSize.Height - textSize.Height - 20 - bottomOffset;

                RectangleF bgRect = new RectangleF(x - 10, y - 5, textSize.Width + 20, textSize.Height + 10);
                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(220, 0, 0, 128)))
                    g.FillRectangle(bgBrush, bgRect);

                using (Pen borderPen = new Pen(Color.FromArgb(255, 0, 255, 255), 2))
                    g.DrawRectangle(borderPen, bgRect.X, bgRect.Y, bgRect.Width, bgRect.Height);

                using (SolidBrush textBrush = new SolidBrush(Color.White))
                    g.DrawString(message, font, textBrush, x, y);
            }
        }

        /// <summary>
        /// Draws the Ghost3 countdown message above any existing bottom-anchored banners.
        /// bottomOffset is the total pixel height already consumed by lower banners.
        /// Returns the pixel height consumed by this banner so callers can stack further up.
        /// </summary>
        private int DrawGhost3Countdown(Graphics g, int bottomOffset = 0)
        {
            string message = $"Launching Ghost3 in {_ghost3Countdown} second{(_ghost3Countdown != 1 ? "s" : "")}... Press any key or click to cancel";

            using (Font font = new Font("Consolas", 12f, FontStyle.Bold))
            {
                SizeF textSize = g.MeasureString(message, font);

                float x = (this.ClientSize.Width - textSize.Width) / 2;
                float y = this.ClientSize.Height - textSize.Height - 20 - bottomOffset;

                RectangleF bgRect = new RectangleF(x - 10, y - 5, textSize.Width + 20, textSize.Height + 10);
                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(220, 0, 100, 0)))
                    g.FillRectangle(bgBrush, bgRect);

                using (Pen borderPen = new Pen(Color.FromArgb(255, 0, 255, 0), 2))
                    g.DrawRectangle(borderPen, bgRect.X, bgRect.Y, bgRect.Width, bgRect.Height);

                using (SolidBrush textBrush = new SolidBrush(Color.White))
                    g.DrawString(message, font, textBrush, x, y);

                // Return height consumed: text + bgRect padding (10) + bottom margin (20)
                return (int)(textSize.Height + 30);
            }
        }

        /// <summary>
        /// Draws auto-launch countdown banners stacked from the bottom of the screen.
        /// Returns the total pixel height consumed so callers above can stack further up.
        /// </summary>
        private int DrawAutoLaunchCountdowns(Graphics g)
        {
            if (_autoLaunchCountdowns.Count == 0) return 0;

            var programs = _autoLaunchManager?.GetAllPrograms();
            if (programs == null) return 0;

            int lineNumber = 0;
            int totalHeight = 0;

            using (Font font = new Font("Consolas", 11f, FontStyle.Bold))
            {
                // Measure row height using a representative string
                SizeF sampleSize = g.MeasureString("X", font);
                float rowH = sampleSize.Height + 15;

                foreach (var kvp in _autoLaunchCountdowns)
                {
                    var program = programs.Find(p => p.Id == kvp.Key);
                    if (program == null) continue;

                    string message = $"Launching {program.Name} in {kvp.Value} second{(kvp.Value != 1 ? "s" : "")}...";
                    SizeF textSize = g.MeasureString(message, font);

                    float x = (this.ClientSize.Width - textSize.Width) / 2;
                    float y = this.ClientSize.Height - textSize.Height - 20 - (lineNumber * rowH);

                    RectangleF bgRect = new RectangleF(x - 10, y - 5, textSize.Width + 20, textSize.Height + 10);
                    using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(220, 75, 0, 130)))
                        g.FillRectangle(bgBrush, bgRect);

                    using (Pen borderPen = new Pen(Color.FromArgb(255, 138, 43, 226), 2))
                        g.DrawRectangle(borderPen, bgRect.X, bgRect.Y, bgRect.Width, bgRect.Height);

                    using (SolidBrush textBrush = new SolidBrush(Color.White))
                        g.DrawString(message, font, textBrush, x, y);

                    lineNumber++;
                }

                // Cancel instruction sits above the countdown rows
                if (lineNumber > 0)
                {
                    string cancelMsg = "Press any key or click to cancel all";
                    SizeF textSize = g.MeasureString(cancelMsg, font);
                    float x = (this.ClientSize.Width - textSize.Width) / 2;
                    float y = this.ClientSize.Height - textSize.Height - 20 - (lineNumber * rowH);

                    RectangleF bgRect = new RectangleF(x - 10, y - 5, textSize.Width + 20, textSize.Height + 10);
                    using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
                        g.FillRectangle(bgBrush, bgRect);

                    using (SolidBrush textBrush = new SolidBrush(Color.Yellow))
                        g.DrawString(cancelMsg, font, textBrush, x, y);

                    lineNumber++; // count the cancel row too
                }

                // Return total pixel height consumed so Ghost3/auto-start can stack above
                totalHeight = (int)((lineNumber * rowH) + 20);
            }

            return totalHeight;
        }

        private void MainForm_Resize(object? sender, EventArgs e)
        {
            // Check if we're restoring from minimized
            bool restoringFromMinimized = (_previousWindowState == FormWindowState.Minimized &&
                                           this.WindowState == FormWindowState.Normal);
            _previousWindowState = this.WindowState;

            // Maintain 16:9 aspect ratio (only when not minimized)
            if (this.WindowState == FormWindowState.Normal)
            {
                int targetWidth = this.Width;
                int targetHeight = (int)(targetWidth / 16.0 * 9.0);

                if (this.Height != targetHeight)
                {
                    this.Height = targetHeight;
                }
            }

            // If restoring from minimized, force repaint after a brief delay
            if (restoringFromMinimized)
            {
                this.BeginInvoke((MethodInvoker)delegate
                {
                    this.Invalidate(true);
                    this.Update();
                });
            }
            else
            {
                this.Invalidate();
            }
        }

        private void MainForm_MouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            // Cancel auto-start countdown on any click
            if (_autoStartCountdown > 0)
            {
                CancelAutoStart();
                return;
            }

            // Cancel Ghost3 countdown on any click
            if (_ghost3Countdown > 0)
            {
                CancelGhost3();
                return;
            }

            // Cancel auto-launch countdowns on any click (v1.5 feature)
            if (_autoLaunchCountdowns.Count > 0)
            {
                CancelAllAutoLaunches();
                return;
            }

            // Get click position relative to client area
            float x = e.X / (float)this.ClientSize.Width;
            float y = e.Y / (float)this.ClientSize.Height;

            // Define clickable regions based on background image (1440x810 reference)
            // Left column: Options 1, 2, 3, 4
            if (x >= 0.038f && x <= 0.101f) // x: 54-145 pixels
            {
                if (y >= 0.389f && y <= 0.469f) LaunchOption(1); // Option 1
                else if (y >= 0.519f && y <= 0.599f) LaunchOption(2); // Option 2
                else if (y >= 0.648f && y <= 0.728f) LaunchOption(3); // Option 3
                else if (y >= 0.778f && y <= 0.858f) LaunchOption(4); // Option 4
            }
            // Center column: Options 5, 0
            else if (x >= 0.431f && x <= 0.498f) // x: 621-717 pixels
            {
                if (y >= 0.476f && y <= 0.537f) LaunchOption(5); // Option 5 (Go!)
                else if (y >= 0.667f && y <= 0.728f) LaunchOption(0); // Option 0 (Exit)
            }
            // Right column: Options 6, 7, 8, 99
            else if (x >= 0.687f && x <= 0.750f) // x: 989-1080 pixels
            {
                if (y >= 0.389f && y <= 0.469f) LaunchOption(6); // Option 6
                else if (y >= 0.519f && y <= 0.599f) LaunchOption(7); // Option 7
                else if (y >= 0.648f && y <= 0.728f) LaunchOption(8); // Option 8
                else if (y >= 0.778f && y <= 0.858f) LaunchOption(99); // Option 99
            }
        }

        private void MainForm_MouseMove(object? sender, MouseEventArgs e)
        {
            // Get position relative to client area (normalized 0.0-1.0)
            float x = e.X / (float)this.ClientSize.Width;
            float y = e.Y / (float)this.ClientSize.Height;

            // Show hand cursor when hovering over clickable options
            int optionAtPos = GetOptionAtPosition(x, y);
            this.Cursor = (optionAtPos != -1) ? Cursors.Hand : Cursors.Default;
        }

        private void MainForm_MouseLeave(object? sender, EventArgs e)
        {
            this.Cursor = Cursors.Default;
        }

        /// <summary>
        /// Determines which option (if any) is at the given normalized position.
        /// Returns: -1 = no option, 0-99 = option number
        /// </summary>
        private int GetOptionAtPosition(float x, float y)
        {
            // Left column: Options 1, 2, 3, 4
            if (x >= 0.038f && x <= 0.101f)
            {
                if (y >= 0.389f && y <= 0.469f) return 1;
                if (y >= 0.519f && y <= 0.599f) return 2;
                if (y >= 0.648f && y <= 0.728f) return 3;
                if (y >= 0.778f && y <= 0.858f) return 4;
            }

            // Center column: Options 5, 0
            if (x >= 0.431f && x <= 0.498f)
            {
                if (y >= 0.476f && y <= 0.537f) return 5;
                if (y >= 0.667f && y <= 0.728f) return 0;
            }

            // Right column: Options 6, 7, 8, 99
            if (x >= 0.687f && x <= 0.750f)
            {
                if (y >= 0.389f && y <= 0.469f) return 6;
                if (y >= 0.519f && y <= 0.599f) return 7;
                if (y >= 0.648f && y <= 0.728f) return 8;
                if (y >= 0.778f && y <= 0.858f) return 99;
            }

            return -1; // No option at this position
        }

        /// <summary>
        /// Gets the bounding rectangle for a given option number (in normalized coordinates).
        /// Returns null if the option is not a visual button.
        /// </summary>
        private RectangleF? GetOptionBounds(int option)
        {
            switch (option)
            {
                // Left column
                case 1: return new RectangleF(0.038f, 0.389f, 0.063f, 0.080f);
                case 2: return new RectangleF(0.038f, 0.519f, 0.063f, 0.080f);
                case 3: return new RectangleF(0.038f, 0.648f, 0.063f, 0.080f);
                case 4: return new RectangleF(0.038f, 0.778f, 0.063f, 0.080f);

                // Center column
                case 5: return new RectangleF(0.431f, 0.476f, 0.067f, 0.061f);
                case 0: return new RectangleF(0.431f, 0.667f, 0.067f, 0.061f);

                // Right column
                case 6: return new RectangleF(0.687f, 0.389f, 0.063f, 0.080f);
                case 7: return new RectangleF(0.687f, 0.519f, 0.063f, 0.080f);
                case 8: return new RectangleF(0.687f, 0.648f, 0.063f, 0.080f);
                case 99: return new RectangleF(0.687f, 0.778f, 0.063f, 0.080f);

                default: return null;
            }
        }

        private void MainForm_KeyDown(object? sender, KeyEventArgs e)
        {
            // Cancel auto-start countdown on any key press
            if (_autoStartCountdown > 0)
            {
                CancelAutoStart();
                e.Handled = true;
                return;
            }

            // Cancel Ghost3 countdown on any key press
            if (_ghost3Countdown > 0)
            {
                CancelGhost3();
                e.Handled = true;
                return;
            }

            // Cancel auto-launch countdowns on any key press (v1.5 feature)
            if (_autoLaunchCountdowns.Count > 0)
            {
                CancelAllAutoLaunches();
                e.Handled = true;
                return;
            }

            // F1 opens help dialog
            if (e.KeyCode == Keys.F1)
            {
                OpenHelpDialog();
                e.Handled = true;
                return;
            }

            // F2 launches Enable/Disable Modules (WGSDMOD.exe)
            if (e.KeyCode == Keys.F2)
            {
                LaunchModulesEditor();
                e.Handled = true;
                return;
            }

            // F12 opens configuration editor
            if (e.KeyCode == Keys.F12)
            {
                OpenConfigEditor();
                e.Handled = true;
                return;
            }

            // Number keys 0-9 (handle multi-digit for option 99)
            if (e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9)
            {
                int digit = e.KeyCode - Keys.D0;
                HandleDigitInput(digit);
                e.Handled = true;
                return;
            }

            // NumPad keys 0-9
            if (e.KeyCode >= Keys.NumPad0 && e.KeyCode <= Keys.NumPad9)
            {
                int digit = e.KeyCode - Keys.NumPad0;
                HandleDigitInput(digit);
                e.Handled = true;
                return;
            }

            // Escape key minimizes (to taskbar or tray based on setting)
            if (e.KeyCode == Keys.Escape)
            {
                bool escToTray = _config.GetValue("Settings", "EscMinimizesToTray", "false").ToLower() == "true";
                if (escToTray)
                {
                    MinimizeToTray();
                }
                else
                {
                    this.WindowState = FormWindowState.Minimized;
                }
                e.Handled = true;
                return;
            }

            // F10 closes the program
            if (e.KeyCode == Keys.F10)
            {
                this.Close();
                e.Handled = true;
                return;
            }
        }

        private void HandleDigitInput(int digit)
        {
            // Add digit to buffer
            _digitBuffer += digit.ToString();

            // Restart timer
            _digitTimer?.Stop();
            _digitTimer?.Start();

            // Check if we have a complete option number
            // Option 0 is always immediate (exit)
            if (digit == 0 && _digitBuffer == "0")
            {
                _digitTimer?.Stop();
                _digitBuffer = "";
                LaunchOption(0);
                return;
            }

            // Check for option 99
            if (_digitBuffer == "99")
            {
                _digitTimer?.Stop();
                _digitBuffer = "";
                LaunchOption(99);
                return;
            }

            // Single digit options 1-9 with delay to allow for 99
            if (_digitBuffer.Length == 1 && digit >= 1 && digit <= 9)
            {
                // Wait for potential second digit
                return;
            }

            // If buffer gets too long or invalid, reset
            if (_digitBuffer.Length > 2)
            {
                _digitTimer?.Stop();
                _digitBuffer = "";
            }
        }

        private void DigitTimer_Tick(object? sender, EventArgs e)
        {
            _digitTimer?.Stop();

            // Process single digit if we have one
            if (_digitBuffer.Length == 1 && int.TryParse(_digitBuffer, out int option))
            {
                _digitBuffer = "";
                LaunchOption(option);
            }
            else
            {
                _digitBuffer = "";
            }
        }

        private void LaunchOption(int optionNumber)
        {
            // Option 0 is Exit
            if (optionNumber == 0)
            {
                this.Close();
                return;
            }

            // Get program path from config
            string programCommand = _config.GetValue("Programs", $"Option{optionNumber}");
            string programName = _config.GetValue("Programs", $"Option{optionNumber}Name", $"Option {optionNumber}");

            if (string.IsNullOrWhiteSpace(programCommand))
            {
                MessageBox.Show(
                    $"{programName} is not configured.\n\nPress F12 to configure programs.",
                    "Not Configured",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            // Parse program path and arguments
            string programPath;
            string? arguments = null;

            // Check if there are arguments (look for space after .exe)
            int exeIndex = programCommand.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIndex > 0 && exeIndex + 4 < programCommand.Length)
            {
                programPath = programCommand.Substring(0, exeIndex + 4).Trim();
                arguments = programCommand.Substring(exeIndex + 4).Trim();
            }
            else
            {
                programPath = programCommand.Trim();
            }

            if (!File.Exists(programPath))
            {
                MessageBox.Show(
                    $"Program not found:\n{programPath}\n\nPress F12 to update configuration.",
                    "File Not Found",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            // Get process name from exe path
            string processName = Path.GetFileNameWithoutExtension(programPath);

            // Special handling for wgsappgo which spawns wgserver
            string monitorProcess = processName;
            if (processName.Equals("wgsappgo", StringComparison.OrdinalIgnoreCase))
            {
                monitorProcess = "wgserver";
            }

            // Check if process is already running
            if (ProcessHelper.IsProcessRunning(monitorProcess))
            {
                var result = MessageBox.Show(
                    $"{programName} is already running!\n\nWould you like to bring it to the foreground?",
                    "Already Running",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result == DialogResult.Yes)
                {
                    var process = ProcessHelper.GetProcess(monitorProcess);
                    if (process != null)
                    {
                        ProcessHelper.BringToForeground(process);
                    }
                }
                return;
            }

            // Launch the program
            string? workingDir = Path.GetDirectoryName(programPath);
            var launchedProcess = ProcessHelper.LaunchProgram(programPath, workingDir, arguments);

            if (launchedProcess != null)
            {
                // Update tray status to show running program
                // Use "The Major BBS" for Option 5 instead of "Go!"
                string trayName = (optionNumber == 5) ? "The Major BBS" : programName;
                UpdateTrayStatus(trayName, launchedProcess);

                // Minimize to tray instead of just hiding
                MinimizeToTray();

                // For Option 5 (BBS), start Ghost3 countdown after BBS launches
                if (optionNumber == 5)
                {
                    // Start Ghost3 countdown in the UI thread
                    this.Invoke((MethodInvoker)delegate
                    {
                        StartGhost3Countdown();

                        // Start auto-launch programs (v1.5 feature)
                        _autoLaunchManager?.StartAllLaunches();

                        // Show App Manager if configured
                        bool autoShowAppManager = _config.GetBool("AppManager", "AutoShow", true);
                        if (autoShowAppManager && _appManagerForm != null)
                        {
                            _appManagerForm.Show();
                        }
                    });
                }

                // Watch for the process to exit and clear the tray status.
                // NOTE: AppManager's BBSCrashed event (with its optional delay) is the single
                // path that restores the main launcher window. This task only clears the tray.
                _launchMonitorCts?.Cancel();
                _launchMonitorCts = new System.Threading.CancellationTokenSource();
                var token = _launchMonitorCts.Token;

                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        // For wgsappgo (Option 5), monitor both wgsappgo and wgserver
                        if (processName.Equals("wgsappgo", StringComparison.OrdinalIgnoreCase))
                        {
                            // Wait for wgsappgo launcher to exit
                            launchedProcess.WaitForExit();
                            if (token.IsCancellationRequested) return;

                            // Brief pause to let wgserver spin up, then track it in the tray
                            System.Threading.Thread.Sleep(500);
                            if (token.IsCancellationRequested) return;

                            this.Invoke((MethodInvoker)delegate
                            {
                                if (!this.IsDisposed)
                                {
                                    var serverProcess = ProcessHelper.GetProcess("wgserver");
                                    if (serverProcess != null)
                                        UpdateTrayStatus("WGServer", serverProcess);
                                }
                            });

                            // Wait for wgserver itself to exit
                            while (!token.IsCancellationRequested && ProcessHelper.IsProcessRunning("wgserver"))
                            {
                                var serverProcess = ProcessHelper.GetProcess("wgserver");
                                if (serverProcess != null)
                                    serverProcess.WaitForExit();
                                else
                                    break;
                            }
                        }
                        else
                        {
                            launchedProcess.WaitForExit();
                        }

                        if (token.IsCancellationRequested) return;

                        // Clear tray status only — AppManager handles the restore via BBSCrashed event
                        this.Invoke((MethodInvoker)delegate
                        {
                            if (!this.IsDisposed)
                            {
                                UpdateTrayStatus(null, null);
                                this.Invalidate();
                            }
                        });
                    }
                    catch (Exception)
                    {
                        // Form may have been disposed — nothing to do
                    }
                }, token);
            }
        }

        private void OpenConfigEditor()
        {
            using (var configEditor = new ConfigEditorForm(_config))
            {
                configEditor.ShowDialog(this);
            }

            // Reload config after editing
            _config.LoadConfig();
        }

        private void OpenHelpDialog()
        {
            using (var helpForm = new HelpForm())
            {
                helpForm.ShowDialog(this);
            }
        }

        private void LaunchModulesEditor()
        {
            // Get Module Editor path from config (default to WGSDMOD.exe in BBS path)
            string bbsPath = _config.GetValue("Paths", "BBSPath", @"C:\BBSV10");
            string modulesExe = _config.GetValue("Programs", "ModuleEditor", "");

            // Default to WGSDMOD.exe in BBS path if not configured
            if (string.IsNullOrEmpty(modulesExe))
            {
                modulesExe = Path.Combine(bbsPath, "WGSDMOD.exe");
            }

            if (!File.Exists(modulesExe))
            {
                MessageBox.Show(
                    $"Module Editor not found:\n{modulesExe}\n\nPress F12 to configure the BBS path.",
                    "File Not Found",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            // Check if already running
            if (ProcessHelper.IsProcessRunning("WGSDMOD"))
            {
                var result = MessageBox.Show(
                    "Module Editor is already running!\n\nWould you like to bring it to the foreground?",
                    "Already Running",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result == DialogResult.Yes)
                {
                    var process = ProcessHelper.GetProcess("WGSDMOD");
                    if (process != null)
                    {
                        ProcessHelper.BringToForeground(process);
                    }
                }
                return;
            }

            // Launch the module editor
            var launchedProcess = ProcessHelper.LaunchProgram(modulesExe, bbsPath, null);

            if (launchedProcess != null)
            {
                // Update tray status
                UpdateTrayStatus("Module Editor", launchedProcess);

                // Minimize to tray
                MinimizeToTray();

                // Watch for exit and restore — reuse the shared monitor CTS so FormClosing
                // can cancel this task the same way it cancels the wgserver watcher.
                _launchMonitorCts?.Cancel();
                _launchMonitorCts = new System.Threading.CancellationTokenSource();
                var token = _launchMonitorCts.Token;

                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        launchedProcess.WaitForExit();
                        if (token.IsCancellationRequested || this.IsDisposed) return;
                        this.Invoke((MethodInvoker)delegate
                        {
                            if (!this.IsDisposed)
                            {
                                UpdateTrayStatus(null, null);
                                RestoreFromTray();
                                this.Invalidate();
                                this.Refresh();
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Program.LogError("LaunchModulesEditor.WaitForExit", ex);
                    }
                }, token);
            }
        }

        // Note: Dispose method is in MainForm.Designer.cs
    }
}
