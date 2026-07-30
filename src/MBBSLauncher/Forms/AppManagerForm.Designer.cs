// MBBSLauncher - App Manager Form Designer
// Created by Mark Laudenbach with Love in Iowa
// https://github.com/laudenbachm/MBBS-Launcher
//
// File: Forms/AppManagerForm.Designer.cs
// Version: v1.70
//
// Change History:
// 26.02.11.1 - Initial creation for v1.6 Beta
// 26.02.19.1 - v1.60 - Removed "(Beta)" from title
// 26.02.19.2 - v1.70 - Added opacity/transparency slider; moved checkboxes down to make room
// 26.02.19.3 - v1.70 - Resizable form: Anchor properties on all controls; MinimumSize; red heart label in title bar
// 26.06.30.1 - v2.0-beta10 - CloseButton_Click now calls SaveSettings() so window position is
//                      saved whenever the App Manager is hidden via the X button.

#nullable enable

using System.Drawing;
using System.Windows.Forms;

namespace MBBSLauncher.Forms
{
    partial class AppManagerForm
    {
        private System.ComponentModel.IContainer components = null;
        private Panel titleBarPanel;
        private Label titleLabel;
        private Button closeButton;
        private CheckBox alwaysOnTopCheckbox;
        private CheckBox autoHideCheckbox;
        private Label opacityLabel;
        private TrackBar opacityTrackBar;
        private Label opacityValueLabel;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.titleBarPanel = new Panel();
            this.titleLabel = new Label();
            this.closeButton = new Button();
            this.alwaysOnTopCheckbox = new CheckBox();
            this.autoHideCheckbox = new CheckBox();
            this.opacityLabel = new Label();
            this.opacityTrackBar = new TrackBar();
            this.opacityValueLabel = new Label();

            ((System.ComponentModel.ISupportInitialize)(this.opacityTrackBar)).BeginInit();
            this.SuspendLayout();

            //
            // titleBarPanel
            //
            this.titleBarPanel.BackColor = Color.FromArgb(0, 0, 128);
            this.titleBarPanel.Location = new Point(0, 0);
            this.titleBarPanel.Name = "titleBarPanel";
            this.titleBarPanel.Size = new Size(310, 30);
            this.titleBarPanel.TabIndex = 0;
            this.titleBarPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.titleBarPanel.MouseDown += (s, e) => this.OnMouseDown(e);
            this.titleBarPanel.Cursor = Cursors.SizeAll;

            //
            // titleLabel
            //
            this.titleLabel.AutoSize = false;
            this.titleLabel.BackColor = Color.Transparent;
            this.titleLabel.Font = new Font("Consolas", 10F, FontStyle.Bold);
            this.titleLabel.ForeColor = Color.Cyan;
            this.titleLabel.Location = new Point(10, 6);
            this.titleLabel.Name = "titleLabel";
            this.titleLabel.Size = new Size(200, 18);
            this.titleLabel.TabIndex = 0;
            this.titleLabel.Text = "MBBS App Manager";
            this.titleLabel.MouseDown += (s, e) => this.OnMouseDown(e);
            this.titleLabel.Cursor = Cursors.SizeAll;

            //
            // closeButton
            //
            this.closeButton.BackColor = Color.FromArgb(0, 0, 128);
            this.closeButton.FlatStyle = FlatStyle.Flat;
            this.closeButton.FlatAppearance.BorderSize = 0;
            this.closeButton.Font = new Font("Consolas", 10F, FontStyle.Bold);
            this.closeButton.ForeColor = Color.White;
            this.closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.closeButton.Location = new Point(280, 3);
            this.closeButton.Name = "closeButton";
            this.closeButton.Size = new Size(25, 24);
            this.closeButton.TabIndex = 1;
            this.closeButton.Text = "×";
            this.closeButton.UseVisualStyleBackColor = false;
            this.closeButton.Click += CloseButton_Click;
            this.closeButton.FlatAppearance.MouseOverBackColor = Color.Red;
            this.closeButton.Cursor = Cursors.Hand;

            //
            // alwaysOnTopCheckbox
            //
            this.alwaysOnTopCheckbox.AutoSize = true;
            this.alwaysOnTopCheckbox.BackColor = Color.Transparent;
            this.alwaysOnTopCheckbox.Font = new Font("Consolas", 8F);
            this.alwaysOnTopCheckbox.ForeColor = Color.White;
            this.alwaysOnTopCheckbox.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            this.alwaysOnTopCheckbox.Location = new Point(10, 212);
            this.alwaysOnTopCheckbox.Name = "alwaysOnTopCheckbox";
            this.alwaysOnTopCheckbox.Size = new Size(110, 17);
            this.alwaysOnTopCheckbox.TabIndex = 2;
            this.alwaysOnTopCheckbox.Text = "Always on top";
            this.alwaysOnTopCheckbox.UseVisualStyleBackColor = false;
            this.alwaysOnTopCheckbox.CheckedChanged += AlwaysOnTopCheckbox_CheckedChanged;
            this.alwaysOnTopCheckbox.Checked = true;

            //
            // autoHideCheckbox
            //
            this.autoHideCheckbox.AutoSize = true;
            this.autoHideCheckbox.BackColor = Color.Transparent;
            this.autoHideCheckbox.Font = new Font("Consolas", 8F);
            this.autoHideCheckbox.ForeColor = Color.White;
            this.autoHideCheckbox.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            this.autoHideCheckbox.Location = new Point(130, 212);
            this.autoHideCheckbox.Name = "autoHideCheckbox";
            this.autoHideCheckbox.Size = new Size(140, 17);
            this.autoHideCheckbox.TabIndex = 3;
            this.autoHideCheckbox.Text = "Auto-hide when done";
            this.autoHideCheckbox.UseVisualStyleBackColor = false;
            this.autoHideCheckbox.CheckedChanged += AutoHideCheckbox_CheckedChanged;
            this.autoHideCheckbox.Checked = false;

            //
            // opacityLabel
            //
            this.opacityLabel.AutoSize = false;
            this.opacityLabel.BackColor = Color.Transparent;
            this.opacityLabel.Font = new Font("Consolas", 8F);
            this.opacityLabel.ForeColor = Color.White;
            this.opacityLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            this.opacityLabel.Location = new Point(8, 184);
            this.opacityLabel.Name = "opacityLabel";
            this.opacityLabel.Size = new Size(52, 16);
            this.opacityLabel.TabIndex = 4;
            this.opacityLabel.Text = "Opacity:";

            //
            // opacityTrackBar
            //
            this.opacityTrackBar.AutoSize = false;
            this.opacityTrackBar.BackColor = Color.FromArgb(0, 0, 128);
            this.opacityTrackBar.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            this.opacityTrackBar.Location = new Point(62, 179);
            this.opacityTrackBar.Maximum = 100;
            this.opacityTrackBar.Minimum = 20;
            this.opacityTrackBar.Name = "opacityTrackBar";
            this.opacityTrackBar.Size = new Size(165, 26);
            this.opacityTrackBar.SmallChange = 5;
            this.opacityTrackBar.LargeChange = 10;
            this.opacityTrackBar.TickFrequency = 10;
            this.opacityTrackBar.TabIndex = 5;
            this.opacityTrackBar.Value = 60;
            this.opacityTrackBar.Scroll += OpacityTrackBar_Scroll;

            //
            // opacityValueLabel
            //
            this.opacityValueLabel.AutoSize = false;
            this.opacityValueLabel.BackColor = Color.Transparent;
            this.opacityValueLabel.Font = new Font("Consolas", 8F);
            this.opacityValueLabel.ForeColor = Color.Cyan;
            this.opacityValueLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            this.opacityValueLabel.Location = new Point(229, 184);
            this.opacityValueLabel.Name = "opacityValueLabel";
            this.opacityValueLabel.Size = new Size(40, 16);
            this.opacityValueLabel.TabIndex = 6;
            this.opacityValueLabel.Text = "60%";
            this.opacityValueLabel.TextAlign = ContentAlignment.MiddleRight;

            //
            // AppManagerForm
            //
            this.MinimumSize = new Size(310, 220);
            this.AutoScaleDimensions = new SizeF(6F, 13F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.BackColor = Color.FromArgb(0, 0, 128);
            this.ClientSize = new Size(310, 240);
            this.Controls.Add(this.opacityValueLabel);
            this.Controls.Add(this.opacityTrackBar);
            this.Controls.Add(this.opacityLabel);
            this.Controls.Add(this.autoHideCheckbox);
            this.Controls.Add(this.alwaysOnTopCheckbox);
            this.Controls.Add(this.closeButton);
            this.Controls.Add(this.titleLabel);
            this.Controls.Add(this.titleBarPanel);
            this.FormBorderStyle = FormBorderStyle.None;
            this.Name = "AppManagerForm";
            this.Opacity = 0.95;
            this.StartPosition = FormStartPosition.Manual;
            this.Text = "MBBS App Manager";
            this.TopMost = true;
            this.ShowInTaskbar = false;

            // Add controls to title bar panel
            this.titleBarPanel.Controls.Add(this.titleLabel);
            this.titleBarPanel.Controls.Add(this.closeButton);

            ((System.ComponentModel.ISupportInitialize)(this.opacityTrackBar)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void CloseButton_Click(object? sender, System.EventArgs e)
        {
            this.Hide();
            SaveSettings();
        }

        private void AlwaysOnTopCheckbox_CheckedChanged(object? sender, System.EventArgs e)
        {
            _alwaysOnTop = alwaysOnTopCheckbox.Checked;
            this.TopMost = _alwaysOnTop;
            SaveSettings();
        }

        private void AutoHideCheckbox_CheckedChanged(object? sender, System.EventArgs e)
        {
            _autoHide = autoHideCheckbox.Checked;
            SaveSettings();
        }

        private void OpacityTrackBar_Scroll(object? sender, System.EventArgs e)
        {
            int pct = opacityTrackBar.Value;
            opacityValueLabel.Text = $"{pct}%";
            this.Opacity = pct / 100.0;
            SaveSettings();
        }
    }
}
