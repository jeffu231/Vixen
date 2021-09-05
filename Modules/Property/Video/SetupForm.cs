using System;
using System.Windows.Forms;
using Common.Controls;
using Common.Controls.Theme;
using Common.Resources.Properties;

namespace VixenModules.Property.Video
{
	internal partial class SetupForm : BaseForm
	{
		public SetupForm(int width, int height)
		{
			InitializeComponent();
			ForeColor = ThemeColorTable.ForeColor;
			BackColor = ThemeColorTable.BackgroundColor;
			ThemeUpdateControls.UpdateControls(this);
			_Width = width;
			_Height = height;
            buttonOK.Enabled = true;
        }

		public int SelectedWidth { get; private set; }

		public int SelectedHeight { get; private set; }

		private int _Width
		{
			get { return (int) nudWidth.Value; }
			set { nudWidth.Value = value; }
		}

		private int _Height
		{
			get { return (int) nudHeight.Value; }
			set { nudHeight.Value = value; }
		}

		private void nudWidth_ValueChanged(object sender, EventArgs e)
		{
		}

		private void nudHeight_ValueChanged(object sender, EventArgs e)
		{
		}

		private void nudWidth_Leave(object sender, EventArgs e)
		{
		}

		private void nudHeight_Leave(object sender, EventArgs e)
		{
		}

		private void buttonOK_Click(object sender, EventArgs e)
		{
			SelectedWidth = _Width;
			SelectedHeight = _Height;
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