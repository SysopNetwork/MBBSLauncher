// MBBSLauncher - Configuration Editor Form (v1.5 Redesign)
// Created by Mark Laudenbach with Love in Iowa
// https://github.com/SysopNetwork/MBBSLauncher
//
// File: Forms/ConfigEditorForm.cs
// Version: v2.0
//
// Change History:
// 26.01.07.1 - 06:00PM - Initial creation
// 26.01.12.1 - Added Auto-Start BBS settings and F2 Module Editor option
// 26.01.23.1 - Added Ghost3 support settings
// 26.02.06.1 - v1.5: Complete redesign with TabControl (5 tabs)
// 26.02.19.1 - v1.60: Column sizing on Auto-Launch tab; Auto-Start label height fix;
//              Advanced tab: removed duplicate GitHub URL, added Support section;
//              LoadConfiguration: default AutoLaunchAtStartup checkbox for new installs
// 26.06.04.1 - v1.85: Fixed CreateSectionLabel — separator Label was created but never added
//              to the parent tab; all section divider lines were invisible in the Config Editor
// 26.06.22.1 - v1.90: Removed the "BBS Stop Delay (Cleanup)" setting and its numeric control.
//              Cleanup is now detected automatically (wgsappgo supervisor stays alive), so the
//              manual delay is obsolete.
// 26.06.22.2 - v2.0:  Added the "BBS Monitoring" tab ([Monitoring] settings) with a TICKLER module
//              requirement note + repo link, two-stage controls, and configurable timing knobs.
// 26.06.22.3 - v2.0:  Made the config window taller (730 high) so the BBS Monitoring tab fits
//              without scrolling.
// 26.06.29.1 - v2.0-beta2: TICKLER file check — block enabling BBS Monitoring if TICKLER.DLL /
//              TICKLER.MDF are absent from the configured BBS folder; offer to open the download
//              page at https://github.com/SysopNetwork/TICKLER.
// 26.06.30.1 - v2.0-beta3: Updated BBS Monitoring file check to look for SNTICKLR.DLL and
//              SNTICKLR.MDF instead of TICKLER.DLL and TICKLER.MDF.
// 26.06.30.2 - v2.0-beta6: Enabling BBS Monitoring now auto-installs SNTICKLR.DLL / SNTICKLR.MDF
//              from the MBBS Module folder beside the launcher into the BBS folder. Disabling it
//              removes those files from the BBS folder. GitHub link removed.
// 26.06.30.3 - v2.0-beta7: All Stage 1 / Stage 2 / Timing controls are grouped in a panel that
//              is disabled when "Enable BBS Monitoring" is unchecked, preventing configuration of
//              settings that would have no effect.
// 26.06.30.4 - v2.0-beta8: Enabling or disabling BBS Monitoring is blocked while the BBS is
//              running. A clear alert explains why and the checkbox is automatically reverted.
// 26.06.30.5 - v2.0-beta9: Enabling path uses IsUpdateNeeded() / embedded resource install
//              instead of the old SourceFilesExist() / file-copy approach.
// 26.07.03.1 - v2.0-beta11: General tab: new "App Manager" section with a checkbox that controls
//              whether the App Manager window pops up automatically when the BBS starts
//              ([AppManager] AutoShow, default true). Manual open via the tray menu is unaffected.
// 26.07.03.2 - v2.0-beta12: Advanced tab: new "Software Update" section with a "Check for Updates"
//              button. Manual-only — clicking it queries GitHub for the newest release and reports
//              whether a newer version exists, with the download page URL. The launcher never checks
//              on its own; there is no startup or timer trigger. See Core/UpdateChecker.cs.
// 26.07.03.3 - v2.0-beta13: Advanced tab: new "Extras" section with an "Enable hidden features"
//              checkbox ([Settings] EasterEggsEnabled, default true) plus a light hint. Controls the
//              Konami greetz screen and the CGA/CRT overlay (see MainForm.EasterEggs.cs).

using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using MBBSLauncher.Core;
using MBBSLauncher.Models;

namespace MBBSLauncher.Forms
{
    public partial class ConfigEditorForm : Form
    {
        private ConfigManager _config;
        private TabControl? _tabControl;

        // General tab controls
        private TextBox? _bbsPathTextBox;
        private CheckBox? _autoLaunchCheckBox;
        private CheckBox? _showTrayIconCheckBox;
        private CheckBox? _minimizeToTrayCheckBox;
        private CheckBox? _escToTrayCheckBox;
        private CheckBox? _showAppManagerCheckBox;

        // Advanced tab controls
        private CheckBox? _easterEggsCheckBox;

        // Menu Options tab controls
        private TextBox[] _programTextBoxes = new TextBox[8];
        private TextBox? _program99TextBox;
        private TextBox? _moduleEditorTextBox;

        // Auto-Start tab controls
        private CheckBox? _autoStartBBSCheckBox;
        private NumericUpDown? _autoStartDelayNumeric;
        private CheckBox? _quietModeCheckBox;

        // BBS Monitoring tab (v2.0)
        private CheckBox? _monitorEnabledCheckBox;
        private Panel? _monitorSettingsPanel;   // contains all Stage 1/2/Timing controls; disabled as a unit when monitoring is off
        private bool _monitorToggleInProgress;  // re-entrancy guard for MonitorEnabled_CheckedChanged
        private NumericUpDown? _restartAttemptsNumeric;
        private NumericUpDown? _restartCountdownNumeric;
        private CheckBox? _rebootEnabledCheckBox;
        private NumericUpDown? _rebootCountdownNumeric;
        private NumericUpDown? _crashConfirmNumeric;
        private NumericUpDown? _startupGraceNumeric;
        private NumericUpDown? _attemptDelayNumeric;
        private NumericUpDown? _healthyResetNumeric;

        public ConfigEditorForm(ConfigManager config)
        {
            _config = config;
            InitializeComponent();
            InitializeCustomControls();
            LoadConfiguration();

            // Wire monitoring checkbox after LoadConfiguration so it doesn't fire during init
            if (_monitorEnabledCheckBox != null)
                _monitorEnabledCheckBox.CheckedChanged += MonitorEnabled_CheckedChanged;
        }

        private void InitializeCustomControls()
        {
            this.Text = $"{Program.APP_NAME} {Program.APP_VERSION} - Configuration";
            // Taller window so the BBS Monitoring tab (its tallest) fits without scrolling.
            this.Size = new Size(750, 730);
            this.MinimumSize = new Size(700, 690);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            LoadApplicationIcon();

            // Top panel with header and buttons
            Panel topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 90,
                BackColor = SystemColors.Control,
                Padding = new Padding(10)
            };

            Label versionLabel = new Label
            {
                Text = $"{Program.APP_NAME} {Program.APP_VERSION}",
                Location = new Point(15, 15),
                Size = new Size(450, 25),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 102, 204)
            };
            topPanel.Controls.Add(versionLabel);

            LinkLabel githubLink = new LinkLabel
            {
                Text = Program.GITHUB_URL,
                Location = new Point(15, 40),
                Size = new Size(450, 20),
                LinkColor = Color.FromArgb(0, 102, 204)
            };
            githubLink.LinkClicked += (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = Program.GITHUB_URL,
                        UseShellExecute = true
                    });
                }
                catch { }
            };
            topPanel.Controls.Add(githubLink);

            Label authorLabel = new Label
            {
                Text = $"Created with Love \u2764 by {Program.AUTHOR} in Iowa, USA.",
                Location = new Point(15, 62),
                Size = new Size(450, 22),
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(220, 20, 60)
            };
            topPanel.Controls.Add(authorLabel);

            Button saveBtn = new Button
            {
                Text = "Save",
                Size = new Size(80, 28),
                Location = new Point(520, 30),
                DialogResult = DialogResult.OK
            };
            saveBtn.Click += SaveButton_Click;
            topPanel.Controls.Add(saveBtn);

            Button cancelBtn = new Button
            {
                Text = "Cancel",
                Size = new Size(80, 28),
                Location = new Point(610, 30),
                DialogResult = DialogResult.Cancel
            };
            topPanel.Controls.Add(cancelBtn);

            this.Controls.Add(topPanel);
            this.AcceptButton = saveBtn;
            this.CancelButton = cancelBtn;

            // Create TabControl
            _tabControl = new TabControl
            {
                Location = new Point(10, 100),
                Size = new Size(720, 530),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            // Create tabs
            CreateGeneralTab();
            CreateMenuOptionsTab();
            CreateAutoStartTab();
            CreateAutoLaunchTab();
            CreateBBSMonitoringTab();
            CreateAdvancedTab();

            this.Controls.Add(_tabControl);
        }

        private void CreateGeneralTab()
        {
            var tab = new TabPage("General");
            _tabControl?.TabPages.Add(tab);

            int y = 20;

            // BBS Installation section
            CreateSectionLabel("BBS Installation", y, tab);
            y += 30;

            var lblBBSPath = new Label { Text = "BBS Path:", Location = new Point(20, y), Size = new Size(100, 20) };
            tab.Controls.Add(lblBBSPath);

            _bbsPathTextBox = new TextBox { Location = new Point(130, y), Size = new Size(400, 20) };
            tab.Controls.Add(_bbsPathTextBox);

            var btnBrowse = new Button { Text = "Browse...", Location = new Point(540, y - 2), Size = new Size(80, 24) };
            btnBrowse.Click += (s, e) => BrowseForFolder(_bbsPathTextBox);
            tab.Controls.Add(btnBrowse);
            y += 40;

            // Startup Behavior section
            CreateSectionLabel("Startup Behavior", y, tab);
            y += 30;

            _autoLaunchCheckBox = new CheckBox
            {
                Text = "Launch MBBSLauncher automatically at Windows startup",
                Location = new Point(20, y),
                Size = new Size(600, 25)
            };
            tab.Controls.Add(_autoLaunchCheckBox);
            y += 40;

            // System Tray Behavior section
            CreateSectionLabel("System Tray Behavior", y, tab);
            y += 30;

            _showTrayIconCheckBox = new CheckBox
            {
                Text = "Show icon in system tray",
                Location = new Point(20, y),
                Size = new Size(600, 25)
            };
            tab.Controls.Add(_showTrayIconCheckBox);
            y += 30;

            _minimizeToTrayCheckBox = new CheckBox
            {
                Text = "Minimize to tray when programs are running",
                Location = new Point(20, y),
                Size = new Size(600, 25)
            };
            tab.Controls.Add(_minimizeToTrayCheckBox);
            y += 30;

            _escToTrayCheckBox = new CheckBox
            {
                Text = "ESC key minimizes to tray (instead of taskbar)",
                Location = new Point(20, y),
                Size = new Size(600, 25)
            };
            tab.Controls.Add(_escToTrayCheckBox);
            y += 40;

            // App Manager section
            CreateSectionLabel("App Manager", y, tab);
            y += 30;

            _showAppManagerCheckBox = new CheckBox
            {
                Text = "Show App Manager window automatically when the BBS starts",
                Location = new Point(20, y),
                Size = new Size(600, 25)
            };
            tab.Controls.Add(_showAppManagerCheckBox);

            var lblAppManagerNote = new Label
            {
                Text = "When disabled, the App Manager is still available from the tray icon's right-click menu.",
                Location = new Point(38, y + 25),
                Size = new Size(600, 20),
                Font = new Font("Segoe UI", 8),
                ForeColor = SystemColors.GrayText
            };
            tab.Controls.Add(lblAppManagerNote);
            y += 65;

            // Keyboard Shortcuts section
            CreateSectionLabel("Keyboard Shortcuts", y, tab);
            y += 30;

            string shortcuts = "• F1  - Help\n• F2  - Enable/Disable Modules\n• F12 - Configuration\n• ESC - Minimize or Exit\n• 0-9, 99 - Launch menu options";
            var lblShortcuts = new Label
            {
                Text = shortcuts,
                Location = new Point(20, y),
                Size = new Size(600, 80),
                Font = new Font("Segoe UI", 9)
            };
            tab.Controls.Add(lblShortcuts);
        }

        private void CreateMenuOptionsTab()
        {
            var tab = new TabPage("Menu Options");
            _tabControl?.TabPages.Add(tab);

            var lblInfo = new Label
            {
                Text = "Configure the 8 launcher menu options, special option 99, and F2 module editor.",
                Location = new Point(10, 10),
                Size = new Size(680, 30),
                Font = new Font("Segoe UI", 9)
            };
            tab.Controls.Add(lblInfo);

            var scrollPanel = new Panel
            {
                Location = new Point(10, 45),
                Size = new Size(690, 450),
                AutoScroll = true,
                BorderStyle = BorderStyle.FixedSingle
            };

            string[] optionNames = new string[]
            {
                "Hardware Setup",
                "Design Menu Tree",
                "Security & Accounting",
                "Configuration Options",
                "Go!",
                "Edit Text Blocks",
                "Basic Utilities",
                "Reports"
            };

            int innerY = 10;
            for (int i = 0; i < 8; i++)
            {
                var lblOption = new Label
                {
                    Text = $"{i + 1} - {optionNames[i]}",
                    Location = new Point(10, innerY),
                    Size = new Size(200, 20),
                    Font = new Font("Segoe UI", 9, FontStyle.Bold)
                };
                scrollPanel.Controls.Add(lblOption);

                var lblProgram = new Label
                {
                    Text = "Program:",
                    Location = new Point(220, innerY),
                    Size = new Size(60, 20)
                };
                scrollPanel.Controls.Add(lblProgram);

                _programTextBoxes[i] = new TextBox
                {
                    Location = new Point(280, innerY),
                    Size = new Size(280, 20)
                };
                scrollPanel.Controls.Add(_programTextBoxes[i]);

                var btnBrowse = new Button
                {
                    Text = "Browse...",
                    Location = new Point(570, innerY - 2),
                    Size = new Size(80, 24),
                    Tag = _programTextBoxes[i]
                };
                btnBrowse.Click += BrowseProgramButton_Click;
                scrollPanel.Controls.Add(btnBrowse);

                innerY += 35;
            }

            // Option 99
            var lbl99 = new Label
            {
                Text = "99 - CNF 99",
                Location = new Point(10, innerY),
                Size = new Size(200, 20),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            scrollPanel.Controls.Add(lbl99);

            var lblProgram99 = new Label
            {
                Text = "Program:",
                Location = new Point(220, innerY),
                Size = new Size(60, 20)
            };
            scrollPanel.Controls.Add(lblProgram99);

            _program99TextBox = new TextBox
            {
                Location = new Point(280, innerY),
                Size = new Size(280, 20)
            };
            scrollPanel.Controls.Add(_program99TextBox);

            var btnBrowse99 = new Button
            {
                Text = "Browse...",
                Location = new Point(570, innerY - 2),
                Size = new Size(80, 24),
                Tag = _program99TextBox
            };
            btnBrowse99.Click += BrowseProgramButton_Click;
            scrollPanel.Controls.Add(btnBrowse99);
            innerY += 35;

            // F2 Module Editor
            var lblF2 = new Label
            {
                Text = "F2 - Enable / Disable Modules",
                Location = new Point(10, innerY),
                Size = new Size(200, 20),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            scrollPanel.Controls.Add(lblF2);

            var lblProgramF2 = new Label
            {
                Text = "Program:",
                Location = new Point(220, innerY),
                Size = new Size(60, 20)
            };
            scrollPanel.Controls.Add(lblProgramF2);

            _moduleEditorTextBox = new TextBox
            {
                Location = new Point(280, innerY),
                Size = new Size(280, 20)
            };
            scrollPanel.Controls.Add(_moduleEditorTextBox);

            var btnBrowseF2 = new Button
            {
                Text = "Browse...",
                Location = new Point(570, innerY - 2),
                Size = new Size(80, 24),
                Tag = _moduleEditorTextBox
            };
            btnBrowseF2.Click += BrowseProgramButton_Click;
            scrollPanel.Controls.Add(btnBrowseF2);

            tab.Controls.Add(scrollPanel);
        }

        private void CreateAutoStartTab()
        {
            var tab = new TabPage("Auto-Start");
            _tabControl?.TabPages.Add(tab);

            int y = 20;

            // Auto-Start BBS section
            CreateSectionLabel("Auto-Start BBS", y, tab);
            y += 30;

            _autoStartBBSCheckBox = new CheckBox
            {
                Text = "Automatically start BBS (Option 5) when launcher opens",
                Location = new Point(20, y),
                Size = new Size(600, 25)
            };
            tab.Controls.Add(_autoStartBBSCheckBox);
            y += 35;

            var lblDelay = new Label
            {
                Text = "Delay:",
                Location = new Point(40, y),
                Size = new Size(45, 20)
            };
            tab.Controls.Add(lblDelay);

            _autoStartDelayNumeric = new NumericUpDown
            {
                Location = new Point(85, y),
                Size = new Size(50, 20),
                Minimum = 0,
                Maximum = 60,
                Value = 5
            };
            tab.Controls.Add(_autoStartDelayNumeric);

            var lblSeconds = new Label
            {
                Text = "seconds (0-60)",
                Location = new Point(140, y),
                Size = new Size(100, 20)
            };
            tab.Controls.Add(lblSeconds);
            y += 30;

            _quietModeCheckBox = new CheckBox
            {
                Text = "Quiet mode (minimize to tray after auto-start)",
                Location = new Point(40, y),
                Size = new Size(600, 25)
            };
            tab.Controls.Add(_quietModeCheckBox);
            y += 30;

            var lblInfo1 = new Label
            {
                Text = "ℹ When enabled, the BBS will start automatically after the delay.\n  Press any key or click to cancel during countdown.",
                Location = new Point(20, y),
                Size = new Size(650, 45),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(100, 100, 100)
            };
            tab.Controls.Add(lblInfo1);
            y += 55;
        }

        private void CreateAutoLaunchTab()
        {
            var tab = new TabPage("Auto-Launch");
            _tabControl?.TabPages.Add(tab);

            int y = 20;

            // Section header
            CreateSectionLabel("Auto-Launch Programs After BBS Starts", y, tab);
            y += 30;

            var lblDescription = new Label
            {
                Text = "Configure programs to automatically launch after the BBS starts.\n" +
                       "Each program launches independently based on its delay setting.",
                Location = new Point(20, y),
                Size = new Size(660, 40),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(80, 80, 80)
            };
            tab.Controls.Add(lblDescription);
            y += 50;

            // DataGridView for programs list.
            // Width is kept to 680px so it fits within the tab page content area (~700px usable).
            var gridView = new DataGridView
            {
                Location = new Point(20, y),
                Size = new Size(680, 280),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
            };

            // Add columns.  All small columns use explicit pixel widths; the Path column
            // uses Fill mode so it expands to consume any remaining grid width.
            var colEnabled = new DataGridViewCheckBoxColumn
            {
                Name = "Enabled",
                HeaderText = "Enabled",
                Width = 65,
                MinimumWidth = 55
            };
            gridView.Columns.Add(colEnabled);

            var colName = new DataGridViewTextBoxColumn
            {
                Name = "Name",
                HeaderText = "Program Name",
                ReadOnly = true,
                Width = 130,
                MinimumWidth = 80
            };
            gridView.Columns.Add(colName);

            var colPath = new DataGridViewTextBoxColumn
            {
                Name = "Path",
                HeaderText = "Executable Path",
                ReadOnly = true,
                MinimumWidth = 100,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            };
            gridView.Columns.Add(colPath);

            var colArgs = new DataGridViewTextBoxColumn
            {
                Name = "Arguments",
                HeaderText = "Arguments",
                ReadOnly = true,
                Width = 90,
                MinimumWidth = 50
            };
            gridView.Columns.Add(colArgs);

            var colDelay = new DataGridViewTextBoxColumn
            {
                Name = "Delay",
                HeaderText = "Delay (s)",
                ReadOnly = true,
                Width = 70,
                MinimumWidth = 55
            };
            gridView.Columns.Add(colDelay);

            var colMinimized = new DataGridViewCheckBoxColumn
            {
                Name = "Minimized",
                HeaderText = "Min.",
                Width = 55,
                MinimumWidth = 45
            };
            gridView.Columns.Add(colMinimized);

            // Store hidden ID column
            var colId = new DataGridViewTextBoxColumn
            {
                Name = "Id",
                HeaderText = "Id",
                Visible = false
            };
            gridView.Columns.Add(colId);

            tab.Controls.Add(gridView);
            gridView.Tag = "AutoLaunchGrid"; // For finding later
            y += 290;

            // Buttons
            int btnX = 20;

            var btnAdd = new Button
            {
                Text = "Add Program",
                Location = new Point(btnX, y),
                Size = new Size(120, 30)
            };
            btnAdd.Click += (s, e) => AutoLaunchGrid_AddProgram(gridView);
            tab.Controls.Add(btnAdd);
            btnX += 130;

            var btnEdit = new Button
            {
                Text = "Edit Program",
                Location = new Point(btnX, y),
                Size = new Size(120, 30)
            };
            btnEdit.Click += (s, e) => AutoLaunchGrid_EditProgram(gridView);
            tab.Controls.Add(btnEdit);
            btnX += 130;

            var btnDelete = new Button
            {
                Text = "Delete Program",
                Location = new Point(btnX, y),
                Size = new Size(120, 30)
            };
            btnDelete.Click += (s, e) => AutoLaunchGrid_DeleteProgram(gridView);
            tab.Controls.Add(btnDelete);
            btnX += 150;

            var btnMoveUp = new Button
            {
                Text = "Move Up",
                Location = new Point(btnX, y),
                Size = new Size(100, 30)
            };
            btnMoveUp.Click += (s, e) => AutoLaunchGrid_MoveUp(gridView);
            tab.Controls.Add(btnMoveUp);
            btnX += 110;

            var btnMoveDown = new Button
            {
                Text = "Move Down",
                Location = new Point(btnX, y),
                Size = new Size(100, 30)
            };
            btnMoveDown.Click += (s, e) => AutoLaunchGrid_MoveDown(gridView);
            tab.Controls.Add(btnMoveDown);

            // Load programs into grid
            LoadAutoLaunchPrograms(gridView);
        }

        private void CreateAdvancedTab()
        {
            var tab = new TabPage("Advanced");
            _tabControl?.TabPages.Add(tab);

            int y = 20;

            CreateSectionLabel("Window Settings", y, tab);
            y += 30;

            var lblWindowInfo = new Label
            {
                Text = "Default window size: 960 x 540 (16:9 ratio)\n" +
                       "Minimum size: 640 x 360\n" +
                       "Window position is automatically saved on exit.",
                Location = new Point(20, y),
                Size = new Size(650, 60),
                Font = new Font("Segoe UI", 9)
            };
            tab.Controls.Add(lblWindowInfo);
            y += 70;

            CreateSectionLabel("About", y, tab);
            y += 30;

            var lblAbout = new Label
            {
                Text = $"{Program.APP_NAME} {Program.APP_VERSION}\n" +
                       $"Created by {Program.AUTHOR} with Love \u2764 in Iowa, USA.\n\n" +
                       ".NET Runtime: 8.0 (self-contained)\n" +
                       "Architecture: x86 (32-bit)\n" +
                       "Zero external dependencies!",
                Location = new Point(20, y),
                Size = new Size(650, 120),
                Font = new Font("Segoe UI", 9)
            };
            tab.Controls.Add(lblAbout);
            y += 130;

            // Software Update section (v2.0-beta12) — manual-only check against GitHub releases.
            CreateSectionLabel("Software Update", y, tab);
            y += 30;

            var lblUpdateInfo = new Label
            {
                Text = "Check GitHub for a newer version of MBBSLauncher. This only checks and\n" +
                       "reports back — it never downloads or installs anything on its own, and it\n" +
                       "only runs when you click the button below.",
                Location = new Point(20, y),
                Size = new Size(650, 66),
                Font = new Font("Segoe UI", 9)
            };
            tab.Controls.Add(lblUpdateInfo);
            y += 68;

            var checkUpdatesBtn = new Button
            {
                Text = "Check for Updates",
                Location = new Point(20, y),
                Size = new Size(160, 30),
                Font = new Font("Segoe UI", 9)
            };
            checkUpdatesBtn.Click += CheckForUpdatesButton_Click;
            tab.Controls.Add(checkUpdatesBtn);
            y += 44;

            // Extras section (v2.0-beta13) — toggle for the hidden features.
            CreateSectionLabel("Extras", y, tab);
            y += 30;

            _easterEggsCheckBox = new CheckBox
            {
                Text = "Enable hidden features",
                Location = new Point(20, y),
                Size = new Size(600, 25)
            };
            tab.Controls.Add(_easterEggsCheckBox);

            var lblEasterEggNote = new Label
            {
                Text = "There's a little fun tucked into the main screen. Some things are more fun to\n" +
                       "discover than to be told — but old-school gamers already know what to press.\n" +
                       "Uncheck this to keep the launcher all business.",
                Location = new Point(38, y + 25),
                Size = new Size(620, 55),
                Font = new Font("Segoe UI", 8),
                ForeColor = SystemColors.GrayText
            };
            tab.Controls.Add(lblEasterEggNote);
            y += 95;

            // Support section
            CreateSectionLabel("Support", y, tab);
            y += 30;

            var lblSupportInfo = new Label
            {
                Text = "Questions, feedback, or need help? Join our community:",
                Location = new Point(20, y),
                Size = new Size(650, 20),
                Font = new Font("Segoe UI", 9)
            };
            tab.Controls.Add(lblSupportInfo);
            y += 26;

            var lblDiscord = new Label
            {
                Text = "Sysop Network Discord:",
                Location = new Point(20, y),
                Size = new Size(170, 20),
                Font = new Font("Segoe UI", 9)
            };
            tab.Controls.Add(lblDiscord);

            var discordLink = new LinkLabel
            {
                Text = Program.DISCORD_URL,
                Location = new Point(195, y),
                Size = new Size(450, 20),
                LinkColor = Color.FromArgb(0, 102, 204)
            };
            discordLink.LinkClicked += (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = Program.DISCORD_URL,
                        UseShellExecute = true
                    });
                }
                catch { }
            };
            tab.Controls.Add(discordLink);
            y += 26;

            var lblGitHub = new Label
            {
                Text = "GitHub:",
                Location = new Point(20, y),
                Size = new Size(170, 20),
                Font = new Font("Segoe UI", 9)
            };
            tab.Controls.Add(lblGitHub);

            var githubSupportLink = new LinkLabel
            {
                Text = Program.GITHUB_URL,
                Location = new Point(195, y),
                Size = new Size(450, 20),
                LinkColor = Color.FromArgb(0, 102, 204)
            };
            githubSupportLink.LinkClicked += (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = Program.GITHUB_URL,
                        UseShellExecute = true
                    });
                }
                catch { }
            };
            tab.Controls.Add(githubSupportLink);
        }

        /// <summary>
        /// Manual "Check for Updates" handler. Queries GitHub for the newest release and reports the
        /// result to the sysop. This is the ONLY place the update check is triggered — the launcher
        /// never contacts GitHub on its own. Nothing is downloaded or installed; if an update exists
        /// the sysop is offered the release page to download it themselves.
        /// </summary>
        private async void CheckForUpdatesButton_Click(object? sender, EventArgs e)
        {
            var button = sender as Button;
            string? originalText = button?.Text;

            try
            {
                if (button != null)
                {
                    button.Enabled = false;
                    button.Text = "Checking...";
                }
                this.Cursor = Cursors.WaitCursor;

                UpdateCheckResult result =
                    await UpdateChecker.CheckForUpdatesAsync(Program.APP_VERSION);

                if (!result.Success)
                {
                    MessageBox.Show(this,
                        result.ErrorMessage ?? "The update check could not be completed.",
                        "Check for Updates",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                if (result.UpdateAvailable)
                {
                    string message =
                        "A newer version of MBBSLauncher is available.\n\n" +
                        $"Installed:  {result.CurrentVersion}\n" +
                        $"Available:  {result.LatestVersion}" +
                        (result.IsPrerelease ? "  (pre-release)" : "") + "\n\n" +
                        "Download page:\n" +
                        $"{result.DownloadUrl}\n\n" +
                        "Downloading and replacing the launcher is a manual step — this check does\n" +
                        "not update anything for you.\n\n" +
                        "Open the download page in your web browser now?";

                    var choice = MessageBox.Show(this, message,
                        "Update Available",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (choice == DialogResult.Yes && !string.IsNullOrWhiteSpace(result.DownloadUrl))
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = result.DownloadUrl,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            Program.LogError("CheckForUpdatesButton_Click.OpenUrl", ex);
                            MessageBox.Show(this,
                                "Could not open your web browser automatically.\n\n" +
                                "Please visit this address to download the update:\n\n" +
                                result.DownloadUrl,
                                "Update Available",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }
                    }
                }
                else if (result.RunningNewerThanPublished)
                {
                    MessageBox.Show(this,
                        "You are running a newer build than the latest published release.\n\n" +
                        $"Installed:          {result.CurrentVersion}\n" +
                        $"Latest published:   {result.LatestVersion}\n\n" +
                        "No update is needed.",
                        "Check for Updates",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(this,
                        "You are running the latest version of MBBSLauncher.\n\n" +
                        $"Installed:  {result.CurrentVersion}",
                        "Check for Updates",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            finally
            {
                this.Cursor = Cursors.Default;
                if (button != null)
                {
                    button.Enabled = true;
                    if (originalText != null)
                        button.Text = originalText;
                }
            }
        }

        private void CreateBBSMonitoringTab()
        {
            var tab = new TabPage("BBS Monitoring") { AutoScroll = true };
            _tabControl?.TabPages.Add(tab);

            int y = 20;

            CreateSectionLabel("BBS Monitoring && Auto-Restart", y, tab);
            y += 30;

            // SNTICKLR module note
            var lblSnticklr = new Label
            {
                Text = "ℹ Uses the SNTICKLR module, which writes a heartbeat file (TICKLER.RUN) to the BBS\n" +
                       "  folder while running and removes it on a clean shutdown/cleanup. MBBSLauncher uses\n" +
                       "  that file to tell a crash apart from a normal shutdown. The module is installed and\n" +
                       "  removed automatically when monitoring is enabled or disabled.",
                Location = new Point(20, y),
                Size = new Size(660, 68),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(100, 100, 100)
            };
            tab.Controls.Add(lblSnticklr);
            y += 78;

            _monitorEnabledCheckBox = new CheckBox
            {
                Text = "Enable BBS Monitoring (detect crashes and auto-restart)",
                Location = new Point(20, y),
                Size = new Size(600, 25),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            tab.Controls.Add(_monitorEnabledCheckBox);
            y += 38;

            // All Stage 1 / Stage 2 / Timing controls live in this panel so they can be
            // enabled or disabled as a unit when monitoring is toggled on or off.
            _monitorSettingsPanel = new Panel
            {
                Location = new Point(0, y),
                Size = new Size(700, 380)
            };
            tab.Controls.Add(_monitorSettingsPanel);

            int py = 0; // y relative to _monitorSettingsPanel

            // --- Stage 1 ---
            CreateSectionLabel("Stage 1 — Restart the BBS software", py, _monitorSettingsPanel);
            py += 30;

            _monitorSettingsPanel.Controls.Add(new Label { Text = "Restart attempts:", Location = new Point(40, py), Size = new Size(120, 20) });
            _restartAttemptsNumeric = new NumericUpDown
            {
                Location = new Point(165, py),
                Size = new Size(50, 20),
                Minimum = 1,
                Maximum = 3,
                Value = 3
            };
            _monitorSettingsPanel.Controls.Add(_restartAttemptsNumeric);
            _monitorSettingsPanel.Controls.Add(new Label { Text = "(1-3)", Location = new Point(220, py), Size = new Size(60, 20) });
            py += 30;

            _monitorSettingsPanel.Controls.Add(new Label { Text = "Countdown before restart:", Location = new Point(40, py), Size = new Size(165, 20) });
            _restartCountdownNumeric = new NumericUpDown
            {
                Location = new Point(210, py),
                Size = new Size(55, 20),
                Minimum = 0,
                Maximum = 300,
                Value = 30
            };
            _monitorSettingsPanel.Controls.Add(_restartCountdownNumeric);
            _monitorSettingsPanel.Controls.Add(new Label { Text = "seconds (cancelable)", Location = new Point(270, py), Size = new Size(160, 20) });
            py += 38;

            // --- Stage 2 ---
            CreateSectionLabel("Stage 2 — Reboot the computer (if Stage 1 fails)", py, _monitorSettingsPanel);
            py += 30;

            _rebootEnabledCheckBox = new CheckBox
            {
                Text = "If all restart attempts fail, reboot the computer once",
                Location = new Point(40, py),
                Size = new Size(600, 25)
            };
            _monitorSettingsPanel.Controls.Add(_rebootEnabledCheckBox);
            py += 30;

            _monitorSettingsPanel.Controls.Add(new Label { Text = "Countdown before reboot:", Location = new Point(40, py), Size = new Size(165, 20) });
            _rebootCountdownNumeric = new NumericUpDown
            {
                Location = new Point(210, py),
                Size = new Size(55, 20),
                Minimum = 5,
                Maximum = 600,
                Value = 30
            };
            _monitorSettingsPanel.Controls.Add(_rebootCountdownNumeric);
            _monitorSettingsPanel.Controls.Add(new Label { Text = "seconds (cancelable)", Location = new Point(270, py), Size = new Size(160, 20) });
            py += 38;

            // --- Timing ---
            CreateSectionLabel("Timing", py, _monitorSettingsPanel);
            py += 30;

            _crashConfirmNumeric = AddTimingRow(_monitorSettingsPanel, ref py, "Confirm crash for:", 10, 2, 120, "seconds before acting");
            _startupGraceNumeric = AddTimingRow(_monitorSettingsPanel, ref py, "Wait for BBS to come up:", 60, 10, 600, "seconds = success");
            _attemptDelayNumeric = AddTimingRow(_monitorSettingsPanel, ref py, "Delay between attempts:", 15, 0, 300, "seconds");
            _healthyResetNumeric = AddTimingRow(_monitorSettingsPanel, ref py, "Healthy uptime resets reboot guard:", 10, 1, 120, "minutes");
        }

        /// <summary>
        /// Enables or disables all Stage 1 / Stage 2 / Timing controls as a unit based on
        /// whether "Enable BBS Monitoring" is checked. Settings that have no effect when
        /// monitoring is off cannot be interacted with.
        /// </summary>
        private void UpdateMonitoringControlStates()
        {
            if (_monitorSettingsPanel != null)
                _monitorSettingsPanel.Enabled = _monitorEnabledCheckBox?.Checked ?? false;
        }

        /// <summary>Adds a "label [numeric] suffix" row to a tab and returns the NumericUpDown.</summary>
        private NumericUpDown AddTimingRow(Control tab, ref int y, string label, int value, int min, int max, string suffix)
        {
            tab.Controls.Add(new Label { Text = label, Location = new Point(40, y), Size = new Size(230, 20) });
            var numeric = new NumericUpDown
            {
                Location = new Point(275, y),
                Size = new Size(55, 20),
                Minimum = min,
                Maximum = max,
                Value = value
            };
            tab.Controls.Add(numeric);
            tab.Controls.Add(new Label { Text = suffix, Location = new Point(335, y), Size = new Size(200, 20) });
            y += 30;
            return numeric;
        }

        private void CreateSectionLabel(string text, int y, Control parent)
        {
            var label = new Label
            {
                Text = text,
                Location = new Point(10, y),
                Size = new Size(680, 20),
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };

            var line = new Label
            {
                Location = new Point(10, y + 22),
                Size = new Size(680, 1),
                BorderStyle = BorderStyle.Fixed3D
            };

            parent.Controls.Add(label);
            parent.Controls.Add(line);
        }

        private void LoadConfiguration()
        {
            // General tab
            if (_bbsPathTextBox != null)
                _bbsPathTextBox.Text = _config.GetValue("Paths", "BBSPath");

            if (_autoLaunchCheckBox != null)
            {
                // Show checked if already in Windows startup, OR if the config default says to enable it.
                // The latter covers new installs where the registry entry hasn't been written yet.
                bool inStartup = IsInWindowsStartup();
                bool configDefault = _config.GetValue("Settings", "AutoLaunchAtStartup", "false") == "true";
                _autoLaunchCheckBox.Checked = inStartup || configDefault;
            }

            if (_showTrayIconCheckBox != null)
                _showTrayIconCheckBox.Checked = _config.GetValue("Settings", "ShowTrayIcon", "true") == "true";

            if (_minimizeToTrayCheckBox != null)
                _minimizeToTrayCheckBox.Checked = _config.GetValue("Settings", "MinimizeToTray", "true") == "true";

            if (_escToTrayCheckBox != null)
                _escToTrayCheckBox.Checked = _config.GetValue("Settings", "EscMinimizesToTray", "false") == "true";

            if (_showAppManagerCheckBox != null)
                _showAppManagerCheckBox.Checked = _config.GetBool("AppManager", "AutoShow", true);

            if (_easterEggsCheckBox != null)
                _easterEggsCheckBox.Checked = _config.GetBool("Settings", "EasterEggsEnabled", true);

            // Menu Options tab
            for (int i = 0; i < 8; i++)
            {
                _programTextBoxes[i].Text = _config.GetValue("Programs", $"Option{i + 1}");
            }

            if (_program99TextBox != null)
                _program99TextBox.Text = _config.GetValue("Programs", "Option99");

            if (_moduleEditorTextBox != null)
            {
                string bbsPath = _config.GetValue("Paths", "BBSPath", @"C:\BBSV10");
                string moduleEditor = _config.GetValue("Programs", "ModuleEditor", "");
                if (string.IsNullOrEmpty(moduleEditor))
                    moduleEditor = System.IO.Path.Combine(bbsPath, "WGSDMOD.exe");
                _moduleEditorTextBox.Text = moduleEditor;
            }

            // Auto-Start tab
            if (_autoStartBBSCheckBox != null)
                _autoStartBBSCheckBox.Checked = _config.GetValue("Settings", "AutoStartBBS", "false") == "true";

            if (_autoStartDelayNumeric != null)
            {
                if (int.TryParse(_config.GetValue("Settings", "AutoStartDelay", "5"), out int delay))
                    _autoStartDelayNumeric.Value = Math.Max(0, Math.Min(60, delay));
            }

            if (_quietModeCheckBox != null)
                _quietModeCheckBox.Checked = _config.GetValue("Settings", "QuietMode", "false") == "true";

            // BBS Monitoring tab
            if (_monitorEnabledCheckBox != null)
                _monitorEnabledCheckBox.Checked = _config.GetBool("Monitoring", "Enabled", false);
            SetNumeric(_restartAttemptsNumeric, _config.GetInt("Monitoring", "RestartAttempts", 3));
            SetNumeric(_restartCountdownNumeric, _config.GetInt("Monitoring", "RestartCountdownSeconds", 30));
            if (_rebootEnabledCheckBox != null)
                _rebootEnabledCheckBox.Checked = _config.GetBool("Monitoring", "RebootEnabled", false);
            SetNumeric(_rebootCountdownNumeric, _config.GetInt("Monitoring", "RebootCountdownSeconds", 30));
            SetNumeric(_crashConfirmNumeric, _config.GetInt("Monitoring", "CrashConfirmSeconds", 10));
            SetNumeric(_startupGraceNumeric, _config.GetInt("Monitoring", "StartupGraceSeconds", 60));
            SetNumeric(_attemptDelayNumeric, _config.GetInt("Monitoring", "AttemptDelaySeconds", 15));
            SetNumeric(_healthyResetNumeric, _config.GetInt("Monitoring", "HealthyResetMinutes", 10));

            // Reflect initial monitoring enable state in the dependent controls.
            UpdateMonitoringControlStates();
        }

        /// <summary>Sets a NumericUpDown's value, clamped to its Minimum/Maximum range.</summary>
        private static void SetNumeric(NumericUpDown? numeric, int value)
        {
            if (numeric == null) return;
            numeric.Value = Math.Max(numeric.Minimum, Math.Min(numeric.Maximum, value));
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            // Save General settings
            if (_bbsPathTextBox != null)
                _config.SetValue("Paths", "BBSPath", _bbsPathTextBox.Text);

            if (_showTrayIconCheckBox != null)
                _config.SetValue("Settings", "ShowTrayIcon", _showTrayIconCheckBox.Checked.ToString().ToLower());

            if (_minimizeToTrayCheckBox != null)
                _config.SetValue("Settings", "MinimizeToTray", _minimizeToTrayCheckBox.Checked.ToString().ToLower());

            if (_escToTrayCheckBox != null)
                _config.SetValue("Settings", "EscMinimizesToTray", _escToTrayCheckBox.Checked.ToString().ToLower());

            if (_showAppManagerCheckBox != null)
                _config.SetValue("AppManager", "AutoShow", _showAppManagerCheckBox.Checked.ToString().ToLower());

            if (_easterEggsCheckBox != null)
                _config.SetValue("Settings", "EasterEggsEnabled", _easterEggsCheckBox.Checked.ToString().ToLower());

            // Save Menu Options
            for (int i = 0; i < 8; i++)
            {
                _config.SetValue("Programs", $"Option{i + 1}", _programTextBoxes[i].Text);
            }

            if (_program99TextBox != null)
                _config.SetValue("Programs", "Option99", _program99TextBox.Text);

            if (_moduleEditorTextBox != null)
                _config.SetValue("Programs", "ModuleEditor", _moduleEditorTextBox.Text);

            // Save Auto-Start settings
            if (_autoStartBBSCheckBox != null)
                _config.SetValue("Settings", "AutoStartBBS", _autoStartBBSCheckBox.Checked.ToString().ToLower());

            if (_autoStartDelayNumeric != null)
                _config.SetValue("Settings", "AutoStartDelay", ((int)_autoStartDelayNumeric.Value).ToString());

            if (_quietModeCheckBox != null)
                _config.SetValue("Settings", "QuietMode", _quietModeCheckBox.Checked.ToString().ToLower());

            // Save BBS Monitoring settings
            if (_monitorEnabledCheckBox != null)
                _config.SetValue("Monitoring", "Enabled", _monitorEnabledCheckBox.Checked.ToString().ToLower());
            if (_restartAttemptsNumeric != null)
                _config.SetValue("Monitoring", "RestartAttempts", ((int)_restartAttemptsNumeric.Value).ToString());
            if (_restartCountdownNumeric != null)
                _config.SetValue("Monitoring", "RestartCountdownSeconds", ((int)_restartCountdownNumeric.Value).ToString());
            if (_rebootEnabledCheckBox != null)
                _config.SetValue("Monitoring", "RebootEnabled", _rebootEnabledCheckBox.Checked.ToString().ToLower());
            if (_rebootCountdownNumeric != null)
                _config.SetValue("Monitoring", "RebootCountdownSeconds", ((int)_rebootCountdownNumeric.Value).ToString());
            if (_crashConfirmNumeric != null)
                _config.SetValue("Monitoring", "CrashConfirmSeconds", ((int)_crashConfirmNumeric.Value).ToString());
            if (_startupGraceNumeric != null)
                _config.SetValue("Monitoring", "StartupGraceSeconds", ((int)_startupGraceNumeric.Value).ToString());
            if (_attemptDelayNumeric != null)
                _config.SetValue("Monitoring", "AttemptDelaySeconds", ((int)_attemptDelayNumeric.Value).ToString());
            if (_healthyResetNumeric != null)
                _config.SetValue("Monitoring", "HealthyResetMinutes", ((int)_healthyResetNumeric.Value).ToString());

            // Save Auto-Launch programs
            var autoLaunchGrid = FindAutoLaunchGrid();
            if (autoLaunchGrid != null)
            {
                SaveAutoLaunchPrograms(autoLaunchGrid);
            }

            // Handle Windows startup registry
            if (_autoLaunchCheckBox != null)
            {
                bool autoLaunch = _autoLaunchCheckBox.Checked;
                _config.SetValue("Settings", "AutoLaunchAtStartup", autoLaunch.ToString().ToLower());

                try
                {
                    if (autoLaunch)
                        AddToWindowsStartup();
                    else
                        RemoveFromWindowsStartup();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not update Windows startup: {ex.Message}", "Warning",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            _config.SaveConfig();

            MessageBox.Show("Configuration saved successfully!", "Success",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BrowseForFolder(TextBox? textBox)
        {
            if (textBox == null) return;

            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select folder";
                if (!string.IsNullOrEmpty(textBox.Text))
                    dialog.SelectedPath = textBox.Text;

                if (dialog.ShowDialog() == DialogResult.OK)
                    textBox.Text = dialog.SelectedPath;
            }
        }

        private void BrowseProgramButton_Click(object? sender, EventArgs e)
        {
            if (sender is Button btn && btn.Tag is TextBox textBox)
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Filter = "Programs (*.exe;*.bat)|*.exe;*.bat|Executable Files (*.exe)|*.exe|Batch Files (*.bat)|*.bat|All Files (*.*)|*.*";
                    dialog.Title = "Select Program";

                    if (!string.IsNullOrEmpty(textBox.Text))
                    {
                        try
                        {
                            dialog.InitialDirectory = System.IO.Path.GetDirectoryName(textBox.Text);
                            dialog.FileName = System.IO.Path.GetFileName(textBox.Text);
                        }
                        catch { }
                    }

                    if (dialog.ShowDialog() == DialogResult.OK)
                        textBox.Text = dialog.FileName;
                }
            }
        }

        private void LoadApplicationIcon()
        {
            try
            {
                string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "icon.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    this.Icon = new Icon(iconPath);
                    return;
                }

                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceName = "MBBSLauncher.Resources.icon.ico";

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                        this.Icon = new Icon(stream);
                }
            }
            catch (Exception ex)
            {
                Program.LogError("LoadApplicationIcon (ConfigEditor)", ex);
            }
        }

        private void AddToWindowsStartup()
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (key != null)
                {
                    string? exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                        key.SetValue("MBBSLauncher", $"\"{exePath}\"");
                }
            }
        }

        private void RemoveFromWindowsStartup()
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (key != null && key.GetValue("MBBSLauncher") != null)
                    key.DeleteValue("MBBSLauncher", false);
            }
        }

        private bool IsInWindowsStartup()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    return key?.GetValue("MBBSLauncher") != null;
                }
            }
            catch
            {
                return false;
            }
        }

        //=================================================================================
        // BBS Monitoring Tab Helper Methods
        //=================================================================================

        /// <summary>
        /// Fires when the sysop toggles the "Enable BBS Monitoring" checkbox.
        /// Enabling: installs SNTICKLR.DLL / SNTICKLR.MDF from the MBBS Module folder into the
        ///           BBS folder (if not already present). Reverts the checkbox on failure.
        /// Disabling: removes those files from the BBS folder. Source files in the MBBS Module
        ///            folder beside the launcher are never deleted.
        /// </summary>
        private void MonitorEnabled_CheckedChanged(object? sender, EventArgs e)
        {
            if (_monitorEnabledCheckBox == null) return;
            if (_monitorToggleInProgress) return;

            // Block enable/disable while the BBS is running — SNTICKLR files may be locked by
            // the active BBS process and cannot be installed or removed until it is shut down.
            if (ProcessHelper.IsProcessRunning("wgserver"))
            {
                bool tryingToEnable = _monitorEnabledCheckBox.Checked;
                _monitorToggleInProgress = true;
                try { _monitorEnabledCheckBox.Checked = !tryingToEnable; }
                finally { _monitorToggleInProgress = false; }

                MessageBox.Show(
                    "BBS Monitoring cannot be " + (tryingToEnable ? "enabled" : "disabled") + " while the BBS is running.\n\n" +
                    "Please shut down the BBS first, then change this setting.\n\n" +
                    "The SNTICKLR module files are actively used by the running BBS and cannot\n" +
                    "be installed or removed while the BBS is up.",
                    "BBS Is Running",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // Prefer whatever is currently typed in the BBS Path box (may not be saved yet)
            string bbsPath = _bbsPathTextBox?.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(bbsPath))
                bbsPath = _config.GetValue("Paths", "BBSPath", "");

            if (string.IsNullOrEmpty(bbsPath))
            {
                _monitorEnabledCheckBox.Checked = false;
                MessageBox.Show(
                    "Please configure your BBS installation path on the General tab before enabling BBS Monitoring.",
                    "BBS Path Not Set",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (_monitorEnabledCheckBox.Checked)
            {
                // --- Enabling: ensure the correct SNTICKLR version is in the BBS folder ---
                // The BBS-running guard above (beta8) guarantees the BBS is down at this point,
                // so it is always safe to write to the BBS folder here.
                if (Core.TicklerMonitor.IsUpdateNeeded(bbsPath))
                {
                    if (!Core.TicklerMonitor.InstallModule(bbsPath, out string? installError))
                    {
                        _monitorEnabledCheckBox.Checked = false;
                        MessageBox.Show(
                            "The SNTICKLR module could not be installed into your BBS folder.\n\n" +
                            "Error: " + installError + "\n\n" +
                            "Please check that MBBSLauncher has write access to:\n" + bbsPath,
                            "Installation Failed",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        UpdateMonitoringControlStates();
                        return;
                    }
                }
                // IsUpdateNeeded() = false: correct version already present — allow silently.
            }
            else
            {
                // --- Disabling: remove SNTICKLR files from the BBS folder ---
                bool dllExists = System.IO.File.Exists(System.IO.Path.Combine(bbsPath, "SNTICKLR.DLL"));
                bool mdfExists = System.IO.File.Exists(System.IO.Path.Combine(bbsPath, "SNTICKLR.MDF"));

                if (dllExists || mdfExists)
                {
                    if (!Core.TicklerMonitor.UninstallModule(bbsPath, out string? uninstallError))
                    {
                        MessageBox.Show(
                            "The SNTICKLR module files could not be removed from your BBS folder.\n\n" +
                            "Error: " + uninstallError + "\n\n" +
                            "You may remove them manually:\n" +
                            "  • " + System.IO.Path.Combine(bbsPath, "SNTICKLR.DLL") + "\n" +
                            "  • " + System.IO.Path.Combine(bbsPath, "SNTICKLR.MDF"),
                            "Uninstall Failed",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                }
                // Checkbox stays unchecked regardless of whether uninstall succeeded
            }

            // Reflect the final checkbox state in the dependent controls.
            UpdateMonitoringControlStates();
        }

        //=================================================================================
        // Auto-Launch Tab Helper Methods
        //=================================================================================

        private DataGridView? FindAutoLaunchGrid()
        {
            if (_tabControl == null) return null;

            // Find the Auto-Launch tab
            foreach (TabPage tab in _tabControl.TabPages)
            {
                if (tab.Text == "Auto-Launch")
                {
                    // Find the grid control
                    foreach (Control control in tab.Controls)
                    {
                        if (control is DataGridView grid && grid.Tag?.ToString() == "AutoLaunchGrid")
                        {
                            return grid;
                        }
                    }
                }
            }

            return null;
        }

        private void LoadAutoLaunchPrograms(DataGridView grid)
        {
            grid.Rows.Clear();

            var manager = new Core.AutoLaunchManager();
            manager.LoadFromConfig(_config);

            var programs = manager.GetAllPrograms();

            foreach (var program in programs)
            {
                grid.Rows.Add(
                    program.Enabled,
                    program.Name,
                    program.Path,
                    program.Arguments,
                    program.DelaySeconds,
                    program.LaunchMinimized,
                    program.Id
                );
            }
        }

        private void SaveAutoLaunchPrograms(DataGridView grid)
        {
            var manager = new Core.AutoLaunchManager();

            // Build programs list from grid
            foreach (DataGridViewRow row in grid.Rows)
            {
                var program = new Models.AutoLaunchProgram
                {
                    Id = row.Cells["Id"].Value?.ToString() ?? "",
                    Enabled = (bool)(row.Cells["Enabled"].Value ?? false),
                    Name = row.Cells["Name"].Value?.ToString() ?? "",
                    Path = row.Cells["Path"].Value?.ToString() ?? "",
                    Arguments = row.Cells["Arguments"].Value?.ToString() ?? "",
                    DelaySeconds = int.TryParse(row.Cells["Delay"].Value?.ToString(), out int delay) ? delay : 30,
                    LaunchMinimized = (bool)(row.Cells["Minimized"].Value ?? true)
                };

                // Use existing ID or let AddProgram assign one
                if (string.IsNullOrEmpty(program.Id) || program.Id.StartsWith("AutoLaunch"))
                {
                    manager.AddProgram(program);
                }
            }

            manager.SaveToConfig(_config);
        }

        private void AutoLaunchGrid_AddProgram(DataGridView grid)
        {
            using (var dialog = new AutoLaunchProgramDialog())
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    var program = dialog.GetProgram();

                    // Assign temporary ID (will be properly assigned on save)
                    program.Id = $"AutoLaunch{grid.Rows.Count + 1}";

                    grid.Rows.Add(
                        program.Enabled,
                        program.Name,
                        program.Path,
                        program.Arguments,
                        program.DelaySeconds,
                        program.LaunchMinimized,
                        program.Id
                    );
                }
            }
        }

        private void AutoLaunchGrid_EditProgram(DataGridView grid)
        {
            if (grid.SelectedRows.Count == 0)
            {
                MessageBox.Show(
                    "Please select a program to edit.",
                    "No Selection",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var row = grid.SelectedRows[0];
            var program = new Models.AutoLaunchProgram
            {
                Id = row.Cells["Id"].Value?.ToString() ?? "",
                Enabled = (bool)(row.Cells["Enabled"].Value ?? false),
                Name = row.Cells["Name"].Value?.ToString() ?? "",
                Path = row.Cells["Path"].Value?.ToString() ?? "",
                Arguments = row.Cells["Arguments"].Value?.ToString() ?? "",
                DelaySeconds = int.TryParse(row.Cells["Delay"].Value?.ToString(), out int delay) ? delay : 30,
                LaunchMinimized = (bool)(row.Cells["Minimized"].Value ?? true)
            };

            using (var dialog = new AutoLaunchProgramDialog(program))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    var updated = dialog.GetProgram();

                    row.Cells["Enabled"].Value = updated.Enabled;
                    row.Cells["Name"].Value = updated.Name;
                    row.Cells["Path"].Value = updated.Path;
                    row.Cells["Arguments"].Value = updated.Arguments;
                    row.Cells["Delay"].Value = updated.DelaySeconds;
                    row.Cells["Minimized"].Value = updated.LaunchMinimized;
                }
            }
        }

        private void AutoLaunchGrid_DeleteProgram(DataGridView grid)
        {
            if (grid.SelectedRows.Count == 0)
            {
                MessageBox.Show(
                    "Please select a program to delete.",
                    "No Selection",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var row = grid.SelectedRows[0];
            string programName = row.Cells["Name"].Value?.ToString() ?? "this program";

            var result = MessageBox.Show(
                $"Are you sure you want to delete '{programName}'?",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                grid.Rows.Remove(row);
            }
        }

        private void AutoLaunchGrid_MoveUp(DataGridView grid)
        {
            if (grid.SelectedRows.Count == 0)
                return;

            int selectedIndex = grid.SelectedRows[0].Index;
            if (selectedIndex == 0)
                return; // Already at top

            var row = grid.Rows[selectedIndex];
            grid.Rows.RemoveAt(selectedIndex);
            grid.Rows.Insert(selectedIndex - 1, row);
            grid.ClearSelection();
            grid.Rows[selectedIndex - 1].Selected = true;
        }

        private void AutoLaunchGrid_MoveDown(DataGridView grid)
        {
            if (grid.SelectedRows.Count == 0)
                return;

            int selectedIndex = grid.SelectedRows[0].Index;
            if (selectedIndex >= grid.Rows.Count - 1)
                return; // Already at bottom

            var row = grid.Rows[selectedIndex];
            grid.Rows.RemoveAt(selectedIndex);
            grid.Rows.Insert(selectedIndex + 1, row);
            grid.ClearSelection();
            grid.Rows[selectedIndex + 1].Selected = true;
        }
    }
}
