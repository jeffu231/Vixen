namespace VixenModules.SmartController.VideoScreen.WPF
{
	partial class VideoWindowWpfContainer
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
			this.statusStrip = new System.Windows.Forms.StatusStrip();
			this.toolStripStatusPixelsLabel = new System.Windows.Forms.ToolStripStatusLabel();
			this.toolStripStatusPixels = new System.Windows.Forms.ToolStripStatusLabel();
			this.toolStripStatusLabelDistance = new System.Windows.Forms.ToolStripStatusLabel();
			this.toolStripStatusDistance = new System.Windows.Forms.ToolStripStatusLabel();
			this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
			this.toolStripStatusFPS = new System.Windows.Forms.ToolStripStatusLabel();
			this.statusStrip.SuspendLayout();
			this.SuspendLayout();
			// 
			// statusStrip
			// 
			this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusPixelsLabel,
            this.toolStripStatusPixels,
            this.toolStripStatusLabelDistance,
            this.toolStripStatusDistance,
            this.toolStripStatusLabel1,
            this.toolStripStatusFPS});
			this.statusStrip.Location = new System.Drawing.Point(0, 298);
			this.statusStrip.Name = "statusStrip";
			this.statusStrip.Padding = new System.Windows.Forms.Padding(1, 0, 16, 0);
			this.statusStrip.Size = new System.Drawing.Size(475, 22);
			this.statusStrip.TabIndex = 3;
			this.statusStrip.Text = "statusStrip1";
			// 
			// toolStripStatusPixelsLabel
			// 
			this.toolStripStatusPixelsLabel.Name = "toolStripStatusPixelsLabel";
			this.toolStripStatusPixelsLabel.Size = new System.Drawing.Size(0, 17);
			// 
			// toolStripStatusPixels
			// 
			this.toolStripStatusPixels.Name = "toolStripStatusPixels";
			this.toolStripStatusPixels.Size = new System.Drawing.Size(0, 17);
			// 
			// toolStripStatusLabelDistance
			// 
			this.toolStripStatusLabelDistance.Name = "toolStripStatusLabelDistance";
			this.toolStripStatusLabelDistance.Size = new System.Drawing.Size(0, 17);
			// 
			// toolStripStatusDistance
			// 
			this.toolStripStatusDistance.Name = "toolStripStatusDistance";
			this.toolStripStatusDistance.Size = new System.Drawing.Size(0, 17);
			// 
			// toolStripStatusLabel1
			// 
			this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
			this.toolStripStatusLabel1.Size = new System.Drawing.Size(29, 17);
			this.toolStripStatusLabel1.Text = "FPS:";
			// 
			// toolStripStatusFPS
			// 
			this.toolStripStatusFPS.Name = "toolStripStatusFPS";
			this.toolStripStatusFPS.Size = new System.Drawing.Size(19, 17);
			this.toolStripStatusFPS.Text = "20";
			// 
			// VideoPreviewWPFContainer
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(475, 320);
			this.Controls.Add(this.statusStrip);
			this.Name = "VideoWindowWpfContainer";
			this.Text = "VideoPreviewWPFContainer";
			this.statusStrip.ResumeLayout(false);
			this.statusStrip.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.StatusStrip statusStrip;
		private System.Windows.Forms.ToolStripStatusLabel toolStripStatusPixelsLabel;
		private System.Windows.Forms.ToolStripStatusLabel toolStripStatusPixels;
		private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelDistance;
		private System.Windows.Forms.ToolStripStatusLabel toolStripStatusDistance;
		private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
		private System.Windows.Forms.ToolStripStatusLabel toolStripStatusFPS;
	}
}