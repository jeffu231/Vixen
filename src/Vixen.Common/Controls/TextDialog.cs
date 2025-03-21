﻿using Common.Controls.Theme;

namespace Common.Controls
{
	public partial class TextDialog : BaseForm
	{
		public TextDialog(string prompt)
		{
			InitializeComponent();
			ThemeUpdateControls.UpdateControls(this);
			labelPrompt.Text = prompt;
		}

		public TextDialog(string prompt, string title)
			: this(prompt)
		{
			this.Text = title;
		}

		public TextDialog(string prompt, string title, string initialText, bool selectInitialText = false)
			: this(prompt, title)
		{
			textBoxResponse.Text = initialText;
			if (selectInitialText)
				textBoxResponse.SelectAll();
		}

		#region Overrides of Form

		/// <inheritdoc />
		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);
			Activate();
			textBoxResponse.Focus();
		}

		#endregion

		private void TextDialog_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel;
			else if (e.KeyCode == Keys.Enter) DialogResult = DialogResult.OK;
		}

		public string Response
		{
			get { return textBoxResponse.Text; }
		}
	}
}