// MBBSLauncher - Auto-Launch Program Dialog
// Created by Mark Laudenbach with Love in Iowa
// https://github.com/laudenbachm/MBBS-Launcher
//
// File: Forms/AutoLaunchProgramDialog.cs
// Version: v1.5
//
// Change History:
// 26.02.06.1 - Initial creation for v1.5

using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using MBBSLauncher.Models;

namespace MBBSLauncher.Forms
{
    /// <summary>
    /// Dialog for adding or editing an auto-launch program.
    /// </summary>
    public class AutoLaunchProgramDialog : Form
    {
        private TextBox? _nameTextBox;
        private TextBox? _pathTextBox;
        private TextBox? _argsTextBox;
        private NumericUpDown? _delayNumeric;
        private CheckBox? _enabledCheckBox;
        private CheckBox? _minimizedCheckBox;
        private Button? _browseButton;
        private Button? _okButton;
        private Button? _cancelButton;

        private AutoLaunchProgram _program;

        public AutoLaunchProgramDialog() : this(new AutoLaunchProgram
        {
            Name = "",
            Path = "",
            Arguments = "",
            DelaySeconds = 30,
            Enabled = true
        })
        {
        }

        public AutoLaunchProgramDialog(AutoLaunchProgram program)
        {
            _program = program;
            InitializeDialog();
            LoadProgram();
        }

        private void InitializeDialog()
        {
            this.Text = string.IsNullOrEmpty(_program.Name) ? "Add Auto-Launch Program" : "Edit Auto-Launch Program";
            this.Size = new Size(600, 320);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            int y = 20;

            // Enabled checkbox
            var lblEnabled = new Label
            {
                Text = "Enabled:",
                Location = new Point(20, y),
                Size = new Size(100, 20)
            };
            this.Controls.Add(lblEnabled);

            _enabledCheckBox = new CheckBox
            {
                Location = new Point(130, y),
                Size = new Size(400, 20),
                Checked = true
            };
            this.Controls.Add(_enabledCheckBox);
            y += 35;

            // Program Name
            var lblName = new Label
            {
                Text = "Program Name:",
                Location = new Point(20, y),
                Size = new Size(100, 20)
            };
            this.Controls.Add(lblName);

            _nameTextBox = new TextBox
            {
                Location = new Point(130, y),
                Size = new Size(400, 25)
            };
            this.Controls.Add(_nameTextBox);
            y += 35;

            // Executable Path
            var lblPath = new Label
            {
                Text = "Executable Path:",
                Location = new Point(20, y),
                Size = new Size(100, 20)
            };
            this.Controls.Add(lblPath);

            _pathTextBox = new TextBox
            {
                Location = new Point(130, y),
                Size = new Size(350, 25)
            };
            this.Controls.Add(_pathTextBox);

            _browseButton = new Button
            {
                Text = "Browse...",
                Location = new Point(490, y - 2),
                Size = new Size(80, 28)
            };
            _browseButton.Click += BrowseButton_Click;
            this.Controls.Add(_browseButton);
            y += 35;

            // Arguments
            var lblArgs = new Label
            {
                Text = "Arguments:",
                Location = new Point(20, y),
                Size = new Size(100, 20)
            };
            this.Controls.Add(lblArgs);

            _argsTextBox = new TextBox
            {
                Location = new Point(130, y),
                Size = new Size(400, 25),
                PlaceholderText = "Optional command-line arguments"
            };
            this.Controls.Add(_argsTextBox);
            y += 35;

            // Delay
            var lblDelay = new Label
            {
                Text = "Delay (seconds):",
                Location = new Point(20, y),
                Size = new Size(100, 20)
            };
            this.Controls.Add(lblDelay);

            _delayNumeric = new NumericUpDown
            {
                Location = new Point(130, y),
                Size = new Size(100, 25),
                Minimum = 0,
                Maximum = 600,
                Value = 30
            };
            this.Controls.Add(_delayNumeric);

            var lblDelayHelp = new Label
            {
                Text = "Seconds to wait after BBS starts before launching this program",
                Location = new Point(240, y + 3),
                Size = new Size(330, 20),
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8)
            };
            this.Controls.Add(lblDelayHelp);
            y += 40;

            // Launch Minimized checkbox
            _minimizedCheckBox = new CheckBox
            {
                Text = "Launch minimized (doesn't steal focus)",
                Location = new Point(20, y),
                Size = new Size(400, 20),
                Checked = true
            };
            this.Controls.Add(_minimizedCheckBox);
            y += 35;

            // Buttons
            _okButton = new Button
            {
                Text = "OK",
                Location = new Point(370, y),
                Size = new Size(90, 32),
                DialogResult = DialogResult.OK
            };
            _okButton.Click += OkButton_Click;
            this.Controls.Add(_okButton);

            _cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(470, y),
                Size = new Size(90, 32),
                DialogResult = DialogResult.Cancel
            };
            this.Controls.Add(_cancelButton);

            this.AcceptButton = _okButton;
            this.CancelButton = _cancelButton;
        }

        private void LoadProgram()
        {
            if (_enabledCheckBox != null)
                _enabledCheckBox.Checked = _program.Enabled;

            if (_nameTextBox != null)
                _nameTextBox.Text = _program.Name;

            if (_pathTextBox != null)
                _pathTextBox.Text = _program.Path;

            if (_argsTextBox != null)
                _argsTextBox.Text = _program.Arguments;

            if (_delayNumeric != null)
                _delayNumeric.Value = Math.Max(0, Math.Min(600, _program.DelaySeconds));

            if (_minimizedCheckBox != null)
                _minimizedCheckBox.Checked = _program.LaunchMinimized;
        }

        private void BrowseButton_Click(object? sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "Programs (*.exe;*.bat)|*.exe;*.bat|Executable Files (*.exe)|*.exe|Batch Files (*.bat)|*.bat|All Files (*.*)|*.*";
                dialog.Title = "Select Program";

                if (!string.IsNullOrEmpty(_pathTextBox?.Text))
                {
                    try
                    {
                        string? directory = Path.GetDirectoryName(_pathTextBox.Text);
                        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                        {
                            dialog.InitialDirectory = directory;
                        }
                    }
                    catch
                    {
                        // Ignore errors setting initial directory
                    }
                }

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    if (_pathTextBox != null)
                        _pathTextBox.Text = dialog.FileName;

                    // Auto-fill name if empty
                    if (_nameTextBox != null && string.IsNullOrWhiteSpace(_nameTextBox.Text))
                    {
                        _nameTextBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
                    }
                }
            }
        }

        private void OkButton_Click(object? sender, EventArgs e)
        {
            // Validate
            if (string.IsNullOrWhiteSpace(_nameTextBox?.Text))
            {
                MessageBox.Show(
                    "Please enter a program name.",
                    "Validation Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                _nameTextBox?.Focus();
                this.DialogResult = DialogResult.None;
                return;
            }

            if (string.IsNullOrWhiteSpace(_pathTextBox?.Text))
            {
                MessageBox.Show(
                    "Please select an executable path.",
                    "Validation Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                _pathTextBox?.Focus();
                this.DialogResult = DialogResult.None;
                return;
            }

            if (!File.Exists(_pathTextBox.Text))
            {
                var result = MessageBox.Show(
                    $"The file does not exist:\n{_pathTextBox.Text}\n\nDo you want to save anyway?",
                    "File Not Found",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.No)
                {
                    this.DialogResult = DialogResult.None;
                    return;
                }
            }

            // Save to program object
            _program.Enabled = _enabledCheckBox?.Checked ?? true;
            _program.Name = _nameTextBox?.Text ?? "";
            _program.Path = _pathTextBox?.Text ?? "";
            _program.Arguments = _argsTextBox?.Text ?? "";
            _program.DelaySeconds = (int)(_delayNumeric?.Value ?? 30);
            _program.LaunchMinimized = _minimizedCheckBox?.Checked ?? true;
        }

        public AutoLaunchProgram GetProgram()
        {
            return _program;
        }
    }
}
