// MBBSLauncher - Configuration Editor Form (v1.5 Redesign)
// Created by Mark Laudenbach with Love in Iowa
// https://github.com/SysopNetwork/MBBSLauncher
//
// File: Forms/ConfigEditorForm.cs
// Version: v1.85
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

using System;
using System.Drawing;
using System.Windows.Forms;
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

        // Menu Options tab controls
        private TextBox[] _programTextBoxes = new TextBox[8];
        private TextBox? _program99TextBox;
        private TextBox? _moduleEditorTextBox;

        // Auto-Start tab controls
        private CheckBox? _autoStartBBSCheckBox;
        private NumericUpDown? _autoStartDelayNumeric;
        private CheckBox? _quietModeCheckBox;
        private NumericUpDown? _bssStopDelayNumeric;

        public ConfigEditorForm(ConfigManager config)
        {
            _config = config;
            InitializeComponent();
            InitializeCustomControls();
            LoadConfiguration();
        }

        private void InitializeCustomControls()
        {
            this.Text = $"{Program.APP_NAME} {Program.APP_VERSION} - Configuration";
            this.Size = new Size(750, 650);
            this.MinimumSize = new Size(700, 600);
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

            // BBS Stop / Cleanup delay section
            CreateSectionLabel("BBS Stop Delay (Cleanup)", y, tab);
            y += 30;

            var lblStopDelay = new Label
            {
                Text = "Delay after BBS stops:",
                Location = new Point(40, y),
                Size = new Size(160, 20)
            };
            tab.Controls.Add(lblStopDelay);

            _bssStopDelayNumeric = new NumericUpDown
            {
                Location = new Point(205, y),
                Size = new Size(60, 20),
                Minimum = 0,
                Maximum = 600,
                Value = 0
            };
            tab.Controls.Add(_bssStopDelayNumeric);

            var lblStopSeconds = new Label
            {
                Text = "seconds (0 = show immediately)",
                Location = new Point(270, y),
                Size = new Size(230, 20)
            };
            tab.Controls.Add(lblStopSeconds);
            y += 30;

            var lblInfo2 = new Label
            {
                Text = "ℹ Set this to match your BBS cleanup/shutdown time so the launcher\n" +
                       "  does not pop up while cleanup scripts and programs are still running.\n" +
                       "  The delay is automatically cancelled if the BBS restarts during this window.",
                Location = new Point(20, y),
                Size = new Size(650, 55),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(100, 100, 100)
            };
            tab.Controls.Add(lblInfo2);
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

            if (_bssStopDelayNumeric != null)
            {
                if (int.TryParse(_config.GetValue("Settings", "BBSStopDelay", "0"), out int stopDelay))
                    _bssStopDelayNumeric.Value = Math.Max(0, Math.Min(600, stopDelay));
            }
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

            if (_bssStopDelayNumeric != null)
                _config.SetValue("Settings", "BBSStopDelay", ((int)_bssStopDelayNumeric.Value).ToString());

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
