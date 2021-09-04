using System;
using System.Windows.Forms;
using Common.Controls;
using Common.Controls.Theme;
using Common.Resources.Properties;

namespace VixenModules.OutputFilter.PanTiltBreakdown
{
	public partial class PanTiltBreakdownSetup : BaseForm
	{
		private PanTiltBreakdownData _data;
		public PanTiltBreakdownSetup(PanTiltBreakdownData data)
		{
			InitializeComponent();
			ForeColor = ThemeColorTable.ForeColor;
			BackColor = ThemeColorTable.BackgroundColor;
			ThemeUpdateControls.UpdateControls(this);
			_data = data;
			
			checkBoxCanPan.Checked = _data.CanPan;
			checkBoxCanTilt.Checked = _data.CanTilt;
			
		}

		public bool CanPan { get; set; }

		
		public bool CanTilt { get; set; }

	
		private void checkBoxCanPan_CheckedChanged(object sender, System.EventArgs e)
		{
			CanPan = checkBoxCanPan.Checked;
		}

		private void checkBoxCanTilt_CheckedChanged(object sender, System.EventArgs e)
		{
			CanTilt = checkBoxCanTilt.Checked;
		}

		private void buttonBackground_MouseHover(object sender, EventArgs e)
		{
			var btn = (Button)sender;
			btn.BackgroundImage = Resources.ButtonBackgroundImageHover;
		}

		private void buttonBackground_MouseLeave(object sender, EventArgs e)
		{
			var btn = (Button)sender;
			btn.BackgroundImage = Resources.ButtonBackgroundImage;

		}
	}
}
