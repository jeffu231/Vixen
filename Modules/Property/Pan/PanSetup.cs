using System.Windows.Forms;

namespace VixenModules.Property.Pan
{
	public partial class PanSetup : Form
	{
		public PanSetup(int range)
		{
			InitializeComponent();
			Range = range;
			nmRange.Value = range;
		}

		public int Range { get; private set; }

		private void nmRange_ValueChanged(object sender, System.EventArgs e)
		{
			Range = (int)nmRange.Value;
		}
	}
}
