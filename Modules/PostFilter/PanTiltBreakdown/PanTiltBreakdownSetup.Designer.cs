
namespace VixenModules.OutputFilter.PanTiltBreakdown
{
	partial class PanTiltBreakdownSetup
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
			this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
			this.buttonCancel = new System.Windows.Forms.Button();
			this.buttonOk = new System.Windows.Forms.Button();
			this.checkBoxCanPan = new System.Windows.Forms.CheckBox();
			this.checkBoxCanTilt = new System.Windows.Forms.CheckBox();
			this.flowLayoutPanel1.SuspendLayout();
			this.SuspendLayout();
			// 
			// flowLayoutPanel1
			// 
			this.flowLayoutPanel1.Controls.Add(this.buttonCancel);
			this.flowLayoutPanel1.Controls.Add(this.buttonOk);
			this.flowLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Bottom;
			this.flowLayoutPanel1.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
			this.flowLayoutPanel1.Location = new System.Drawing.Point(0, 146);
			this.flowLayoutPanel1.Name = "flowLayoutPanel1";
			this.flowLayoutPanel1.Size = new System.Drawing.Size(383, 35);
			this.flowLayoutPanel1.TabIndex = 2;
			// 
			// buttonCancel
			// 
			this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.buttonCancel.Location = new System.Drawing.Point(305, 3);
			this.buttonCancel.Name = "buttonCancel";
			this.buttonCancel.Size = new System.Drawing.Size(75, 23);
			this.buttonCancel.TabIndex = 0;
			this.buttonCancel.Text = "Cancel";
			this.buttonCancel.UseVisualStyleBackColor = true;
			// 
			// buttonOk
			// 
			this.buttonOk.DialogResult = System.Windows.Forms.DialogResult.OK;
			this.buttonOk.Location = new System.Drawing.Point(224, 3);
			this.buttonOk.Name = "buttonOk";
			this.buttonOk.Size = new System.Drawing.Size(75, 23);
			this.buttonOk.TabIndex = 1;
			this.buttonOk.Text = "Ok";
			this.buttonOk.UseVisualStyleBackColor = true;
			this.buttonOk.MouseLeave += new System.EventHandler(this.buttonBackground_MouseLeave);
			this.buttonOk.MouseHover += new System.EventHandler(this.buttonBackground_MouseHover);
			// 
			// checkBoxCanPan
			// 
			this.checkBoxCanPan.AutoSize = true;
			this.checkBoxCanPan.Location = new System.Drawing.Point(40, 23);
			this.checkBoxCanPan.Name = "checkBoxCanPan";
			this.checkBoxCanPan.Size = new System.Drawing.Size(96, 19);
			this.checkBoxCanPan.TabIndex = 3;
			this.checkBoxCanPan.Text = "Supports Pan";
			this.checkBoxCanPan.UseVisualStyleBackColor = true;
			this.checkBoxCanPan.CheckedChanged += new System.EventHandler(this.checkBoxCanPan_CheckedChanged);
			// 
			// checkBoxCanTilt
			// 
			this.checkBoxCanTilt.AutoSize = true;
			this.checkBoxCanTilt.Location = new System.Drawing.Point(40, 63);
			this.checkBoxCanTilt.Name = "checkBoxCanTilt";
			this.checkBoxCanTilt.Size = new System.Drawing.Size(92, 19);
			this.checkBoxCanTilt.TabIndex = 4;
			this.checkBoxCanTilt.Text = "Supports Tilt";
			this.checkBoxCanTilt.UseVisualStyleBackColor = true;
			this.checkBoxCanTilt.CheckedChanged += new System.EventHandler(this.checkBoxCanTilt_CheckedChanged);
			// 
			// PositionBreakdownSetup
			// 
			this.AcceptButton = this.buttonOk;
			this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.CancelButton = this.buttonCancel;
			this.ClientSize = new System.Drawing.Size(383, 181);
			this.Controls.Add(this.checkBoxCanTilt);
			this.Controls.Add(this.checkBoxCanPan);
			this.Controls.Add(this.flowLayoutPanel1);
			this.Name = "PanTiltBreakdownSetup";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Pan Tilt Breakdown Setup";
			this.flowLayoutPanel1.ResumeLayout(false);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion
		private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
		private System.Windows.Forms.Button buttonCancel;
		private System.Windows.Forms.Button buttonOk;
		private System.Windows.Forms.CheckBox checkBoxCanPan;
		private System.Windows.Forms.CheckBox checkBoxCanTilt;
	}
}