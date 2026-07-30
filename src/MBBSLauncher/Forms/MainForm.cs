// MBBSLauncher - Main Form
// Created by Mark Laudenbach with Love in Iowa
// https://github.com/SysopNetwork/MBBSLauncher
//
// File: Forms/MainForm.cs
// Version: v2.0-beta22
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
// 26.06.22.1 - v1.90 - Fix: auto-start BBS countdown re-triggered after BBS closed. CheckAutoStartBBS
//                      now runs once per session (Shown can re-fire on handle recreation/restore).
// 26.06.22.2 - v1.90 - Fix: MainForm_Paint guarded against late WM_PAINT after the background image
//                      is disposed on shutdown (caused the white box + red X paint failure).
// 26.06.22.3 - v1.90 - RestoreFromTray now forces the launcher to the foreground (TopMost toggle);
//                      non-BBS option programs restore the launcher to front when they close. BBS
//                      restore still routes through the delayed BBSCrashed path (not during cleanup).
// 26.06.22.4 - v1.90 - Renamed user-facing "Ghost3" text to "3rd Party App Launcher" (legacy
//                      countdown code/settings kept intact for backward compatibility).
// 26.06.22.5 - v1.90 - Updated BBS-exit monitor comments: cleanup is now auto-detected via the
//                      wgsappgo supervisor; the manual BBSStopDelay was removed.
// 26.06.22.6 - v2.0  - Wired in BBS Monitoring & Auto-Restart: creates the RestartManager, provides
//                      the restart-the-BBS callback, shows the incident/countdown window, and on
//                      total failure restores the launcher with a crash alert.
// 26.06.22.7 - v2.0  - Added the Btrieve auto-continue watcher (BtrieveDialogWatcher): auto-clicks
//                      Yes on wgsappgo's "Btrieve already running" startup prompt so a post-crash
//                      restart finishes unattended. Gated by [Settings] AutoClickBtrieveContinue.
// 26.06.29.1 - v2.0-beta2: Warn the sysop when closing MBBSLauncher with BBS Monitoring active;
//                      suggest minimizing to the system tray with ESC instead.
// 26.06.30.1 - v2.0-beta4: Startup validation — if BBS Monitoring is enabled but SNTICKLR.DLL /
//                      SNTICKLR.MDF are missing from the BBS folder, monitoring is automatically
//                      disabled and the sysop is alerted. Same check runs when the config editor
//                      closes, guarding against a changed BBS path with no module installed.
// 26.06.30.2 - v2.0-beta6: CheckMonitoringPrerequisites now auto-installs SNTICKLR from the MBBS
//                      Module folder when files are missing (instead of just disabling monitoring).
//                      Only disables if the source files themselves are absent.
// 26.06.30.3 - v2.0-beta9: CheckMonitoringPrerequisites uses version-aware logic: compares
//                      installed SNTICKLR version against ModuleShipVersion and updates silently
//                      when the BBS is down. If BBS is running and files are missing, disables
//                      monitoring. If BBS is running and files are just outdated, logs only.
// 26.07.05.1 - v2.0-beta18: Writes a "Startup" banner to audit.log at launch recording the full
//                      version and the initial BBS/process state (monitoring on/off, wgserver,
//                      wgsappgo, TICKLER.RUN present + resolved path). Pairs with the per-line build
//                      tag added in Program.cs so any later event's build is identifiable at a glance.
// 26.07.08.1 - v2.0-beta19: Fix the launcher popping up on top of a CNF editor after a BBS shutdown.
//                      The BBS-stop restore is delayed (~6s AppManager debounce); if the sysop pops the
//                      launcher and opens a CNF/Module/Offline program in that window, the delayed
//                      BBSCrashed used to slam the launcher over it. AppManager_BBSCrashed now skips the
//                      restore when a launcher-managed child (IsLauncherChildRunning via _runningProcess)
//                      is still open — that program's own exit watcher restores the launcher when it closes.
// 26.07.15.1 - v2.0-beta20: Start an always-on CrashDialogWatcher ([Settings] AutoDismissCrashDialog,
//                      default on). When wgserver faults, its memory/"Application Error" box kept the
//                      dead process listed so crash detection did not fire until a human closed the box;
//                      the watcher now clears it (logging its text) so recovery/restore run unattended.
// 26.07.29.1 - v2.0-beta22: Launcher now reappears ~1.5-2s after a BBS shutdown instead of ~6-8s
//                      (see AppManagerForm for the detection half). Two changes here:
//                      (1) Retract watch — because the BBS-down debounce is now short, AppManager_BBSCrashed
//                      arms a 15s watch ([Settings] BBSRetractWindowSec, 0 disables) after restoring. If
//                      wgserver or wgsappgo comes back in that window the stop was a cleanup, not a
//                      shutdown, so the launcher goes back to the tray and audit.log records the exact
//                      elapsed time (which is also the BBSDownConfirmMs value needed to prevent it).
//                      Cancelled by any key press or click — once the sysop is using the window, taking
//                      it away is worse than leaving it. Skipped if a launcher child program is open.
//                      (2) Double-launch guard — LaunchOption's "already running" check only looked at
//                      wgserver, which is DOWN mid-cleanup while wgsappgo is alive, so pressing Go! then
//                      started a SECOND BBS. It now refuses while the supervisor is up. The new
//                      LaunchOption(silent:) parameter keeps that modal off unattended callers (monitor
//                      recovery, auto-start countdown), which must never block on a message box.

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
        // Guard so the auto-start BBS check runs only once at initial load. Shown can re-fire
        // (e.g. on handle recreation / restore from tray) and must not restart the countdown
        // after the BBS has been closed.
        private bool _autoStartChecked = false;

        // Guard so the monitoring prerequisite check only fires once at startup. It also runs
        // explicitly each time the config editor closes (no guard needed there).
        private bool _monitoringPrereqChecked = false;

        // Ghost3 countdown (v1.20 legacy - kept for compatibility)
        private System.Windows.Forms.Timer? _ghost3Timer;
        private int _ghost3Countdown = 0;
        private bool _ghost3Cancelled = false;

        // Auto-launch programs (v1.5 feature)
        private Core.AutoLaunchManager? _autoLaunchManager;
        private System.Collections.Generic.Dictionary<string, int> _autoLaunchCountdowns = new System.Collections.Generic.Dictionary<string, int>();

        // App Manager
        private AppManagerForm? _appManagerForm;

        // BBS Monitoring & Auto-Restart (v2.0)
        private Core.RestartManager? _restartManager;
        private MonitorIncidentForm? _monitorIncidentForm;

        // Btrieve "already running" auto-continue watcher (v2.0)
        private Core.BtrieveDialogWatcher? _btrieveWatcher;

        // wgserver crash-box auto-dismiss watcher (v2.0-beta20)
        private Core.CrashDialogWatcher? _crashDialogWatcher;

        // Track window state for restore detection
        private FormWindowState _previousWindowState = FormWindowState.Normal;

        // Cancellation for the background wgserver watcher task
        private System.Threading.CancellationTokenSource? _launchMonitorCts;

        // BBS-stop "retract" watch (v2.0-beta22). The BBS-down debounce in AppManagerForm is now short
        // (~1.5s) so the launcher comes back quickly after a shutdown. If that call turns out to be
        // wrong — a cleanup briefly took both wgserver and wgsappgo away and the supervisor then came
        // back — this watch puts the launcher back in the tray instead of leaving it sitting in front
        // of the sysop inviting a mid-cleanup Go!.
        private System.Windows.Forms.Timer? _bbsRetractTimer;
        private readonly System.Diagnostics.Stopwatch _bbsRetractClock = new System.Diagnostics.Stopwatch();
        private int _bbsRetractWindowMs;
        // Window state captured immediately BEFORE the restore, so a retract can put the window back
        // exactly as it was. Calling MinimizeToTray() instead would be wrong: it is a no-op when
        // [Settings] MinimizeToTray=false, which left the launcher sitting in the foreground.
        private FormWindowState _bbsPreRestoreWindowState = FormWindowState.Minimized;
        private bool _bbsPreRestoreVisible;

        public MainForm()
        {
            try
            {
                InitializeComponent();
                _config = new ConfigManager();
                LogStartupBanner(); // first audit.log line of the session: build + initial BBS state
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

            // Initialize hidden features (Konami greetz + CGA/CRT overlay). See MainForm.EasterEggs.cs.
            InitializeEasterEggs();

            // Initialize Auto-Launch Manager (v1.5)
            _autoLaunchManager = new Core.AutoLaunchManager();
            _autoLaunchManager.LoadFromConfig(_config);
            _autoLaunchManager.CountdownTick += AutoLaunchManager_CountdownTick;
            _autoLaunchManager.ProgramLaunched += AutoLaunchManager_ProgramLaunched;
            _autoLaunchManager.AllLaunchesCancelled += AutoLaunchManager_AllLaunchesCancelled;

            // Initialize App Manager
            _appManagerForm = new AppManagerForm(_autoLaunchManager, _config);
            _appManagerForm.BBSCrashed += AppManager_BBSCrashed;

            // Initialize BBS Monitoring & Auto-Restart (v2.0). The manager only acts once it has
            // seen the BBS running, so it is safe to start it unconditionally.
            _restartManager = new Core.RestartManager(Models.AutoRestartSettings.LoadFromConfig(_config), _config);
            _restartManager.LaunchBBS = RestartBBSFromMonitor;
            _restartManager.StatusChanged += Monitor_StatusChanged;
            _restartManager.IncidentResolved += Monitor_IncidentResolved;
            _restartManager.Start();

            // Btrieve auto-continue (v2.0). Standalone of the monitor above: whenever the BBS
            // restarts — by Worldgroup's own recovery, our monitor, or a manual relaunch — wgsappgo
            // shows a Btrieve "already running — continue?" prompt that blocks startup until
            // answered. When enabled, click Yes for it (the watcher only acts while wgserver is down).
            if (_config.GetBool("Settings", "AutoClickBtrieveContinue", true))
            {
                _btrieveWatcher = new Core.BtrieveDialogWatcher();
                _btrieveWatcher.Start();
            }

            // wgserver crash-box auto-dismiss (v2.0-beta20). Standalone always-on watcher: when
            // wgserver faults, its "Application Error"/memory box keeps the dead process listed so
            // neither crash detector fires until a human closes it. This clears the box (logging its
            // text first) so recovery or launcher restore proceeds unattended, whether or not BBS
            // Monitoring is enabled.
            if (_config.GetBool("Settings", "AutoDismissCrashDialog", true))
            {
                _crashDialogWatcher = new Core.CrashDialogWatcher();
                _crashDialogWatcher.Start();
            }

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

            // Force the launcher to actually become the foreground (top) window. Under Windows'
            // foreground-locking rules, Activate()/BringToFront() will not pull focus away from
            // another application's window; briefly toggling TopMost reliably promotes us to the
            // front. This is what makes the launcher resurface on top after a launched program
            // (BBS, CNF, Menu Editor, Offline Utilities, Report Viewer, WGSDMOD) closes.
            bool wasTopMost = this.TopMost;
            this.TopMost = true;
            this.TopMost = wasTopMost;

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
            // When the sysop explicitly closes the launcher (X button, tray Exit, option 0, F10),
            // warn them that BBS Monitoring will stop and suggest minimizing to tray instead.
            if (e.CloseReason == CloseReason.UserClosing ||
                e.CloseReason == CloseReason.ApplicationExitCall)
            {
                bool monitoringEnabled = _config.GetBool("Monitoring", "Enabled", false);
                if (monitoringEnabled)
                {
                    var result = MessageBox.Show(
                        "Closing MBBSLauncher will stop BBS Monitoring and Auto-Restart.\n\n" +
                        "If the BBS crashes or goes down while MBBSLauncher is closed, it will not be " +
                        "automatically detected or restarted.\n\n" +
                        "Tip: Instead of closing, press the ESC key to minimize MBBSLauncher to the " +
                        "system tray. BBS Monitoring continues to run in the background while keeping " +
                        "the launcher out of your way.\n\n" +
                        "Are you sure you want to close MBBSLauncher and stop BBS Monitoring?",
                        "BBS Monitoring Is Active",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2);

                    if (result == DialogResult.No)
                    {
                        e.Cancel = true;
                        return;
                    }
                }
            }

            // Cancel any background process-watcher task so it doesn't Invoke on a disposed form
            _launchMonitorCts?.Cancel();

            // Stop BBS monitoring and tear down its incident window
            _restartManager?.Stop();
            _monitorIncidentForm?.Dispose();
            _btrieveWatcher?.Stop();
            _crashDialogWatcher?.Stop();

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

            // Guard against popping up on top of a program the sysop launched AFTER the BBS went down.
            // The BBS-stop restore is delayed (~6s BBS_DOWN_CONFIRM_TICKS debounce in AppManagerForm),
            // so the sysop can pop the launcher from the tray and open a CNF editor / Module Editor /
            // Offline Utilities in that window. When the delayed BBSCrashed then fires, restoring here
            // would slam the launcher over the program they just opened. If a launcher-managed child is
            // still running, hold the launcher back where it is — that program's own exit watcher
            // (see LaunchOption) restores the launcher when it closes, so nothing is lost.
            if (IsLauncherChildRunning())
            {
                Program.LogInfo("AppManager.BBSCrashed",
                    $"BBS stop detected, but '{_runningProgramName}' is running — holding the launcher back (skipping restore).");

                // Still surface a passive notification (it does not steal focus) so the sysop knows.
                _trayIcon?.ShowBalloonTip(
                    5000,
                    "BBS has stopped.",
                    "The Major BBS has stopped. The launcher was kept in the background because a program is open.",
                    ToolTipIcon.Warning);
                return;
            }

            // Remember where the window was before we move it, so a retract is an exact undo.
            _bbsPreRestoreWindowState = this.WindowState;
            _bbsPreRestoreVisible = this.Visible;

            // Restore main window from tray
            RestoreFromTray();

            // The restore now happens ~1.5-2s after the processes vanish rather than ~6-8s, which
            // leaves less room for the debounce to be sure. Watch for a moment: if the BBS comes back,
            // this was a cleanup, not a shutdown — put the launcher away again.
            ArmBbsRetractWatch();

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

        #region BBS-stop retract watch (v2.0-beta22)

        /// <summary>
        /// Starts watching for the BBS coming back after we have already restored the launcher on a
        /// "BBS stopped" decision. Window length is [Settings] BBSRetractWindowSec (default 15, 0 disables).
        ///
        /// This is the safety net that lets the AppManager debounce be short. A false "stopped" is now
        /// self-correcting: the launcher flickers up and goes back to the tray, instead of staying in
        /// front of the sysop during a cleanup where pressing Go! would start a second BBS.
        /// </summary>
        private void ArmBbsRetractWatch()
        {
            _bbsRetractWindowMs = Math.Clamp(_config.GetInt("Settings", "BBSRetractWindowSec", 15), 0, 120) * 1000;
            if (_bbsRetractWindowMs <= 0)
                return; // disabled by config

            if (_bbsRetractTimer == null)
            {
                _bbsRetractTimer = new System.Windows.Forms.Timer { Interval = 500 };
                _bbsRetractTimer.Tick += BbsRetractTimer_Tick;
            }

            _bbsRetractClock.Restart();
            _bbsRetractTimer.Start();
        }

        /// <summary>
        /// Stops the retract watch. Called when the window expires, when either BBS process comes back,
        /// and on any sysop interaction with the launcher — once they have touched it, yanking the
        /// window out from under them would be worse than leaving it up.
        /// </summary>
        private void CancelBbsRetractWatch()
        {
            if (_bbsRetractTimer == null || !_bbsRetractTimer.Enabled)
                return;

            _bbsRetractTimer.Stop();
            _bbsRetractClock.Reset();
        }

        private void BbsRetractTimer_Tick(object? sender, EventArgs e)
        {
            if (_bbsRetractClock.ElapsedMilliseconds >= _bbsRetractWindowMs)
            {
                // Window elapsed with the BBS still gone — the stop was real. Nothing to do.
                CancelBbsRetractWatch();
                return;
            }

            bool serverBack = ProcessHelper.IsProcessRunning("wgserver");
            bool supervisorBack = ProcessHelper.IsProcessRunning("wgsappgo");
            if (!serverBack && !supervisorBack)
                return;

            long ms = _bbsRetractClock.ElapsedMilliseconds;
            CancelBbsRetractWatch();

            // The sysop opened something in the meantime — leave the screen alone. That program's own
            // exit watcher owns the launcher's visibility from here.
            if (IsLauncherChildRunning())
            {
                Program.LogInfo("BBS.Retract",
                    $"BBS came back {ms}ms after the stop decision (wgserver={(serverBack ? "up" : "down")} " +
                    $"wgsappgo={(supervisorBack ? "up" : "down")}) but '{_runningProgramName}' is open — leaving the launcher visible.");
                return;
            }

            Program.LogInfo("BBS.Retract",
                $"False BBS-stop retracted: BBS came back {ms}ms after the launcher was restored " +
                $"(wgserver={(serverBack ? "up" : "down")} wgsappgo={(supervisorBack ? "up" : "down")}). " +
                $"Hiding the launcher again. Raise [Settings] BBSDownConfirmMs above {ms} to stop this happening.");

            // Put the window back exactly as it was before the false restore. Note this deliberately
            // does NOT touch UpdateTrayStatus/_runningProcess: pointing _runningProcess at a BBS process
            // would make IsLauncherChildRunning() true and suppress the NEXT real BBS-stop restore.
            if (!_bbsPreRestoreVisible && _trayIcon != null && _trayIcon.Visible)
            {
                this.Hide();
            }
            else
            {
                // Minimize regardless of [Settings] MinimizeToTray — that setting decides where the
                // window goes when the sysop starts a program, not whether we may undo our own mistake.
                var target = _bbsPreRestoreWindowState == FormWindowState.Normal
                    ? FormWindowState.Minimized
                    : _bbsPreRestoreWindowState;
                this.WindowState = target;

                // Re-assert once after the current message batch. RestoreFromTray() sets WindowState,
                // ShowInTaskbar and toggles TopMost, and the resulting resize/activate traffic is still
                // in flight when the retract lands ~500ms later; in ~1 run of 16 the window came back up
                // anyway. Re-applying after layout settles is idempotent when it already worked.
                this.BeginInvoke((MethodInvoker)delegate
                {
                    if (!this.IsDisposed && this.WindowState != target)
                    {
                        Program.LogInfo("BBS.Retract", $"Window state bounced back to {this.WindowState}; re-applying {target}.");
                        this.WindowState = target;
                    }
                });
            }
        }

        #endregion

        /// <summary>
        /// True when a launcher-managed child program (a CNF editor, Module Editor, Offline Utilities,
        /// Report Viewer, etc. — anything launched through LaunchOption/LaunchModulesEditor) is currently
        /// running. Tracked via _runningProcess, which is set on launch and cleared to null when the
        /// program exits. Used to keep the delayed BBS-stop restore from jumping on top of a program the
        /// sysop opened after the BBS went down. A dead/exited handle counts as "not running".
        /// </summary>
        private bool IsLauncherChildRunning()
        {
            var proc = _runningProcess;
            if (proc == null) return false;
            try
            {
                return !proc.HasExited;
            }
            catch
            {
                // Handle stale/disposed — treat as not running.
                return false;
            }
        }

        /// <summary>
        /// Writes the first audit.log line of the session: the full build version and the initial
        /// BBS/process state. Combined with the per-line build tag (Program.APP_VERSION_SHORT), this
        /// lets a sysop confirm which version produced any later event and see the starting conditions
        /// (e.g. whether monitoring was on and whether BBSPath actually resolves to the TICKLER.RUN folder).
        /// </summary>
        private void LogStartupBanner()
        {
            try
            {
                string bbsPath = _config.GetValue("Paths", "BBSPath", "");
                bool monitoring = _config.GetBool("Monitoring", "Enabled", false);
                bool serverUp = ProcessHelper.IsProcessRunning("wgserver");
                bool supervisorUp = ProcessHelper.IsProcessRunning("wgsappgo");
                bool ticklerPresent = Core.TicklerMonitor.Exists(bbsPath);
                string ticklerPath = Core.TicklerMonitor.GetTicklerPath(bbsPath) ?? "(no BBSPath configured)";

                Program.LogInfo("Startup",
                    $"{Program.APP_NAME} {Program.APP_VERSION} started | " +
                    $"monitoring={(monitoring ? "on" : "off")} " +
                    $"wgserver={(serverUp ? "up" : "down")} wgsappgo={(supervisorUp ? "up" : "down")} " +
                    $"ticklerPresent={ticklerPresent} bbsPath=\"{bbsPath}\" ticklerPath=\"{ticklerPath}\"");
            }
            catch (Exception ex)
            {
                Program.LogError("Startup banner", ex);
            }
        }

        // ----- BBS Monitoring & Auto-Restart (v2.0) -----

        /// <summary>
        /// Restart callback handed to the RestartManager. Cleans up any lingering/crashed BBS
        /// processes and error dialogs, then relaunches the BBS exactly like pressing Go! (Option 5).
        /// Returns true if a launch was initiated.
        /// </summary>
        private bool RestartBBSFromMonitor()
        {
            try
            {
                // Clear any zombie wgserver/wgsappgo processes and crash dialogs before relaunching.
                ProcessHelper.CleanupBBSProcesses();
            }
            catch (Exception ex)
            {
                Program.LogError("Monitor.CleanupBeforeRestart", ex);
            }

            try
            {
                LaunchOption(5, silent: true); // same path as the Go! button, but unattended
                return true;
            }
            catch (Exception ex)
            {
                Program.LogError("Monitor.RestartBBS", ex);
                return false;
            }
        }

        private void EnsureMonitorIncidentForm()
        {
            if (_monitorIncidentForm == null || _monitorIncidentForm.IsDisposed)
            {
                _monitorIncidentForm = new MonitorIncidentForm();
                _monitorIncidentForm.CancelRequested += (s, e) => _restartManager?.CancelIncident();
            }
        }

        /// <summary>Updates the incident window each second while a recovery is in progress.</summary>
        private void Monitor_StatusChanged(object? sender, Core.MonitorStatusEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => Monitor_StatusChanged(sender, e)));
                return;
            }

            EnsureMonitorIncidentForm();
            _monitorIncidentForm!.SetStatus(e.Message, e.Cancelable);
        }

        /// <summary>Handles the end of a recovery incident.</summary>
        private void Monitor_IncidentResolved(object? sender, Core.MonitorResultEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => Monitor_IncidentResolved(sender, e)));
                return;
            }

            switch (e.Outcome)
            {
                case Core.MonitorOutcome.Rebooting:
                    // Leave the window up with the reboot message; the machine is going down.
                    _monitorIncidentForm?.SetStatus("Rebooting this computer now...", false);
                    break;

                case Core.MonitorOutcome.Failed:
                    // Every enabled stage exhausted and the BBS is still down — surface it.
                    _monitorIncidentForm?.HideIncident();
                    RestoreFromTray();
                    _trayIcon?.ShowBalloonTip(
                        10000,
                        "BBS is down",
                        "Auto-restart failed — the BBS could not be restarted. Manual attention needed.",
                        ToolTipIcon.Error);
                    break;

                default: // Recovered or Cancelled
                    _monitorIncidentForm?.HideIncident();
                    break;
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

            // Validate BBS Monitoring prerequisites once at startup.
            if (!_monitoringPrereqChecked)
            {
                _monitoringPrereqChecked = true;
                CheckMonitoringPrerequisites();
            }

            // Check if auto-start BBS is enabled
            CheckAutoStartBBS();
        }

        /// <summary>
        /// Ensures the correct SNTICKLR module version is installed in the BBS folder whenever
        /// BBS Monitoring is enabled. On every startup this check runs and silently installs or
        /// updates the module when the BBS is down. If the BBS is already running:
        ///   • Files missing  → disables monitoring and alerts the sysop (can't crash-detect without module).
        ///   • Files outdated → logs only; the update applies on next restart when the BBS is down.
        /// </summary>
        private void CheckMonitoringPrerequisites()
        {
            if (!_config.GetBool("Monitoring", "Enabled", false))
                return;

            string bbsPath = _config.GetValue("Paths", "BBSPath", "");
            if (string.IsNullOrWhiteSpace(bbsPath))
                return;

            if (!Core.TicklerMonitor.IsUpdateNeeded(bbsPath))
                return; // Installed version is current — nothing to do.

            bool filesPresent = System.IO.File.Exists(System.IO.Path.Combine(bbsPath, "SNTICKLR.DLL")) &&
                                System.IO.File.Exists(System.IO.Path.Combine(bbsPath, "SNTICKLR.MDF"));

            if (ProcessHelper.IsWGServerRunning())
            {
                if (!filesPresent)
                {
                    // Module not installed at all and the BBS is already running — monitoring can't
                    // function without the module, so disable it now and tell the sysop.
                    _config.SetValue("Monitoring", "Enabled", "false");
                    _config.SaveConfig();
                    _restartManager?.UpdateSettings(Models.AutoRestartSettings.LoadFromConfig(_config));
                    Program.LogInfo("Monitoring", "BBS Monitoring auto-disabled: SNTICKLR is not installed and the BBS is already running.");
                    MessageBox.Show(
                        "BBS Monitoring has been automatically disabled.\n\n" +
                        "The SNTICKLR module is not installed in your BBS folder and the BBS is already running.\n\n" +
                        "Please shut down the BBS, then re-enable BBS Monitoring in Configuration (F12 → BBS Monitoring tab).",
                        "BBS Monitoring Disabled",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                else
                {
                    // Module is installed but outdated. BBS is running so we can't overwrite it now.
                    // Log it and let the update happen automatically next time the BBS is shut down.
                    Version installed = Core.TicklerMonitor.GetInstalledVersion(bbsPath);
                    Program.LogInfo("Monitoring",
                        $"SNTICKLR update available (v{installed} → v{Core.TicklerMonitor.ModuleShipVersion}). " +
                        "Will update automatically the next time the BBS is shut down.");
                }
                return;
            }

            // BBS is not running — safe to install or update now.
            if (Core.TicklerMonitor.InstallModule(bbsPath, out string? error))
            {
                Program.LogInfo("Monitoring",
                    $"SNTICKLR module installed/updated to v{Core.TicklerMonitor.ModuleShipVersion} in {bbsPath}.");
            }
            else
            {
                _config.SetValue("Monitoring", "Enabled", "false");
                _config.SaveConfig();
                _restartManager?.UpdateSettings(Models.AutoRestartSettings.LoadFromConfig(_config));
                Program.LogInfo("Monitoring", $"BBS Monitoring auto-disabled: could not install SNTICKLR. {error}");
                MessageBox.Show(
                    "BBS Monitoring has been automatically disabled.\n\n" +
                    "The SNTICKLR module could not be installed:\n" + error + "\n\n" +
                    "Please check that MBBSLauncher has write access to:\n" + bbsPath + "\n\n" +
                    "Then re-enable BBS Monitoring in Configuration (F12 → BBS Monitoring tab).",
                    "BBS Monitoring Disabled",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void CheckAutoStartBBS()
        {
            // Auto-start is a startup-only behavior: only ever run this once per session.
            // (The Shown event can fire again when the window handle is recreated or the form
            // is restored from tray; without this guard the countdown would restart every time
            // the BBS closed and the launcher reappeared.)
            if (_autoStartChecked) return;
            _autoStartChecked = true;

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

            // Launch Option 5 (Go!) — unattended countdown, so no modal guards
            LaunchOption(5, silent: true);

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
                    $"3rd party app not found:\n{ghost3Path}\n\nPress F12 to update the path in configuration.",
                    "3rd Party App Not Found",
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
            // A WM_PAINT can still be dispatched while the form is being disposed on shutdown,
            // at which point _backgroundImage has already been disposed. Drawing a disposed
            // image throws, and WinForms renders the failure as a white box with a red X where
            // the launcher used to be. Guard against it and never let a paint exception escape.
            if (this.IsDisposed || this.Disposing) return;

            try
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

                // Hidden features draw on top of everything else. See MainForm.EasterEggs.cs.
                DrawEasterEggs(e.Graphics);
            }
            catch (Exception ex)
            {
                // Swallow paint-time failures (e.g. disposed image during shutdown) so they
                // don't surface as the red-X paint error. Logged for diagnostics only.
                Program.LogError("MainForm_Paint", ex);
            }
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
            string message = $"Launching 3rd Party App in {_ghost3Countdown} second{(_ghost3Countdown != 1 ? "s" : "")}... Press any key or click to cancel";

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
            // The sysop is using the launcher — stop any pending retract from hiding it under them.
            CancelBbsRetractWatch();

            if (e.Button != MouseButtons.Left)
                return;

            // A click closes an open greetz screen before anything else.
            if (HandleEasterEggClick())
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
            // The sysop is using the launcher — stop any pending retract from hiding it under them.
            CancelBbsRetractWatch();

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

            // Hidden features: close an open greetz screen, toggle CRT mode, or track the secret
            // key sequences. Consumes the key only when an egg was open or just triggered.
            if (HandleEasterEggKey(e.KeyCode))
            {
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

        /// <summary>
        /// Launches a configured menu option.
        /// </summary>
        /// <param name="optionNumber">Menu option 1-9 / 99 (0 exits).</param>
        /// <param name="silent">
        /// True when the caller is not a person at the keyboard (monitor recovery, auto-start). Suppresses
        /// the beta22 "BBS busy" modal so an unattended path can never sit blocked on a message box.
        /// </param>
        private void LaunchOption(int optionNumber, bool silent = false)
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
            bool isBBSLaunch = processName.Equals("wgsappgo", StringComparison.OrdinalIgnoreCase);
            if (isBBSLaunch)
            {
                monitorProcess = "wgserver";
            }

            // BBS-only guard: during a cleanup wgserver is DOWN while the wgsappgo supervisor is still
            // alive, so the wgserver-only check below happily lets a second BBS start on top of the one
            // that is mid-restart. Refuse while the supervisor is up — it exits only on a real shutdown.
            if (isBBSLaunch && !ProcessHelper.IsProcessRunning("wgserver")
                && ProcessHelper.IsProcessRunning("wgsappgo"))
            {
                Program.LogInfo("LaunchOption",
                    "Blocked a BBS start: wgserver is down but the wgsappgo supervisor is still running " +
                    $"(cleanup/restart in progress). Starting now would run a second BBS. (silent={silent})");

                // Never put a modal box in front of an unattended caller (monitor recovery, auto-start)
                // — it would block the launcher's UI thread waiting for a click nobody is there to make.
                if (!silent)
                {
                    MessageBox.Show(
                        "The Major BBS is still busy.\n\n" +
                        "The server (wgserver.exe) is down but the Go! supervisor (wgsappgo.exe) is still " +
                        "running, which means a cleanup or restart is in progress.\n\n" +
                        "Starting the BBS now would launch a second copy. Please wait for the cleanup to " +
                        "finish, or for the BBS to shut down completely.",
                        "BBS Busy",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return;
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

                // Watch for the process to exit.
                // BBS (wgsappgo): AppManager's BBSCrashed event is the single path that restores the
                //   launcher. AppManager only fires it on a real shutdown (wgserver AND the wgsappgo
                //   supervisor both gone), so the launcher never pops up during a cleanup/restart
                //   cycle. This task only clears the tray.
                // Non-BBS programs (CNF, Menu Editor, Offline Utilities, Report Viewer, etc.):
                //   restore the launcher to the foreground as soon as the program closes.
                bool isBBS = processName.Equals("wgsappgo", StringComparison.OrdinalIgnoreCase);

                _launchMonitorCts?.Cancel();
                _launchMonitorCts = new System.Threading.CancellationTokenSource();
                var token = _launchMonitorCts.Token;

                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        // For wgsappgo (Option 5), monitor both wgsappgo and wgserver
                        if (isBBS)
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

                        this.Invoke((MethodInvoker)delegate
                        {
                            if (this.IsDisposed) return;

                            UpdateTrayStatus(null, null);

                            if (isBBS)
                            {
                                // BBS process exited — let AppManager's BBSCrashed event handle the
                                // restore. It only fires on a real shutdown (supervisor also gone),
                                // so we never surface during a cleanup cycle.
                                this.Invalidate();
                            }
                            else
                            {
                                // Non-BBS program closed — bring the launcher back to the front.
                                RestoreFromTray();
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

            // Apply any BBS Monitoring changes immediately
            _restartManager?.UpdateSettings(Models.AutoRestartSettings.LoadFromConfig(_config));

            // Re-validate prerequisites: catches a changed BBS path where SNTICKLR is absent.
            CheckMonitoringPrerequisites();
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
