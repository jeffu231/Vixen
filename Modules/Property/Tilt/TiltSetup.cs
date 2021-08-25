using System;
using System.Windows.Forms;

namespace VixenModules.Property.Tilt
{
	public partial class TiltSetup : Form
	{
		public TiltSetup(int range)
		{
			InitializeComponent();
			Range = range;
			nmRange.Value = range;
		}

		private void nmRange_ValueChanged(object sender, EventArgs e)
		{
			Range = (int)nmRange.Value;
		}

		public int Range { get; set; }
	}
}
