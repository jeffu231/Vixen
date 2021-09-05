using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Vixen;
using Vixen.Sys;
using VixenModules.Preview.VideoPreview.Dispatch;
using VixenModules.SmartController.VideoScreen.WPF.View;

namespace VixenModules.SmartController.VideoScreen.WPF
{
	public partial class VideoWindowWpfContainer : Form
	{
		private readonly ElementHost _host;

		private int _width = 800, _height = 600;
		private readonly ImageViewer _imageViewer;

		private long _frameCount;
		private readonly Stopwatch _frameRateTimer = Stopwatch.StartNew();

		private bool _formLoading;
		private bool _needsUpdate = true;
		private bool _isRendering;

		private readonly BitmapIntentHandler _handler = new BitmapIntentHandler();
		private string _displayName = "Vixen Preview";

		public VideoWindowWpfContainer()
		{
			_formLoading = true;
			InitializeComponent();
			_host = new ElementHost{Dock = DockStyle.Fill};
			_imageViewer = new ImageViewer();
			_host.Child = _imageViewer;
			Controls.Add(_host);

		}

		public void Setup(int width, int height)
		{
			_formLoading = true;
			
			_width = width;
			_height = height;
			ClientSize = new Size(_width, _height + statusStrip.Height);
			_imageViewer.Configure(_width, _height);

			_formLoading = false;
		}

		private void UpdateFrameRate()
		{
			_frameCount++;

			if (_frameRateTimer.ElapsedMilliseconds > 999)
			{
				FrameRate = _frameCount;
				_frameCount = 0;
				_frameRateTimer.Restart();
			}
		}

		public long FrameRate { get; private set; }

		public IIntentState[] State { get; set; } = new IIntentState[0];

		public void UpdateWindow()
		{
			if (_formLoading) return;
			if (State?.Length>0)
			{
				OnRenderFrame();
				_needsUpdate = true;
				toolStripStatusFPS.Text = FrameRate.ToString();
			}
			else
			{
				if (_needsUpdate)
				{
					OnRenderFrame();
					_needsUpdate = false;
				}
				toolStripStatusFPS.Text = @"0";
			}
		}

		public string DisplayName
		{
			get { return _displayName; }
			set
			{
				_displayName = value;
				if (InvokeRequired)
				{
					Invoke(new Delegates.GenericDelegate(UpdateDisplayName));
				}
				else
				{
					UpdateDisplayName();
				}

			}
		}

		public void UpdateDisplayName()
		{
			Text = _displayName;
		}


		private void OnRenderFrame()
		{
			//Logging.Debug("Entering RenderFrame");
			if (_isRendering || _formLoading || WindowState == FormWindowState.Minimized) return;

			_isRendering = true;

			if (State?.Length > 0)
			{
				var image = _handler.GetImage(State.FirstOrDefault()); //Deal with combining somehow!
				if (image != null)
				{
					_imageViewer.Update(image);
				}
			}
			else
			{
				_imageViewer.Clear();
			}

			_isRendering = false;

			UpdateFrameRate();
		}
	}
}
