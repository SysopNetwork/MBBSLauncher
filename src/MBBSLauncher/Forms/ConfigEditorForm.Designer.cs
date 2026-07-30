// MBBSLauncher - Configuration Editor Form Designer
// Created by Mark Laudenbach with Love in Iowa
// https://github.com/laudenbachm/MBBS-Launcher
//
// File: Forms/ConfigEditorForm.Designer.cs
// Version: v1.00
//
// Change History:
// 26.01.07.1 - 06:00PM - Initial creation

namespace MBBSLauncher.Forms
{
    partial class ConfigEditorForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.SuspendLayout();
            //
            // ConfigEditorForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(700, 600);
            this.Name = "ConfigEditorForm";
            this.Text = "Configuration Editor";
            this.ResumeLayout(false);
        }

        #endregion
    }
}
