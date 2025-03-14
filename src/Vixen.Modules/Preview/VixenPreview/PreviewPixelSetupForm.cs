﻿using Common.Controls.Theme;

namespace VixenModules.Preview.VixenPreview
{
	public partial class PreviewPixelSetupForm : Form
	{
		public PreviewPixelSetupForm(string prefixName, int startingIndex, int lightSize)
		{
			InitializeComponent();
			ThemeUpdateControls.UpdateControls(this);

			suffixIndexChooser.Minimum = 0;
			suffixIndexChooser.Maximum = Int32.MaxValue;
			suffixIndexChooser.Value = startingIndex;

			lightSizeChooser.Minimum = 1;
			lightSizeChooser.Maximum = 100;
			lightSizeChooser.Value = lightSize;

			txtPrefixName.Text = prefixName;
		}

		public int LightSize
		{
			get { return decimal.ToInt32(lightSizeChooser.Value); }
		}

		public int StartingIndex
		{
			get { return decimal.ToInt32(suffixIndexChooser.Value); }
		}

		public string PrefixName
		{
			get { return txtPrefixName.Text; }
		}
	}
}
