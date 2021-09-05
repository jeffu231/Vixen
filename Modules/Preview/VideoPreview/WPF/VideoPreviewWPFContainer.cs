using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Vixen;
using Vixen.Sys;
using VixenModules.Preview.VideoPreview.Dispatch;
using VixenModules.Preview.VideoPreview.WPF.View;
using VixenModules.Preview.VixenPreview;
using VixenModules.Property.Video;

namespace VixenModules.Preview.VideoPreview.WPF
{
	public partial class VideoPreviewWPFContainer : Form, IDisplayForm
	{
		private readonly ElementHost _host;

		private int _width = 800, _height = 600;
		private readonly ImageViewer _imageViewer;

		private long _frameCount;
		private readonly Stopwatch _frameRateTimer = Stopwatch.StartNew();

		private bool _formLoading;
		private bool _needsUpdate = true;
		private bool _isRendering;

		private Element _videoElement;
		private BitmapIntentHandler _handler = new BitmapIntentHandler();
		private string _displayName = "Vixen Preview";

		public VideoPreviewWPFContainer(VideoPreviewData data, Guid instanceId)
		{
			_formLoading = true;
			Data = data;
			InstanceId = instanceId;
			InitializeComponent();
			_host = new ElementHost{Dock = DockStyle.Fill};
			_imageViewer = new ImageViewer();
			_host.Child = _imageViewer;
			Controls.Add(_host);

		}

		#region Implementation of IDisplayForm

		/// <inheritdoc />
		public VideoPreviewData Data { get; set; }

		/// <inheritdoc />
		public void Setup()
		{
			_formLoading = true;
			var videoNodes = VixenSystem.Nodes.GetLeafNodes().Where(x => x.Properties.Contains(VideoDescriptor.ModuleId));
			var elementNode = videoNodes.FirstOrDefault();
			var videoProperty = elementNode?.Properties.Get(VideoDescriptor.ModuleId) as VideoModule;
			if (videoProperty != null)
			{
				_videoElement = elementNode.Element;
				_width = videoProperty.Width;
				_height = videoProperty.Height;
				ClientSize = new Size(_width, _height + statusStrip.Height);
				_imageViewer.Configure(_width, _height);
			}

			_formLoading = false;
		}

		/// <inheritdoc />
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

		/// <inheritdoc />
		public void UpdatePreview()
		{
			if (_formLoading) return;
			if (VixenSystem.Elements.ElementsHaveState)
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

		/// <inheritdoc />
		public Guid InstanceId { get; set; }

		public void UpdateDisplayName()
		{
			Text = _displayName;
		}


		#endregion

		private void OnRenderFrame()
		{
			//Logging.Debug("Entering RenderFrame");
			if (_isRendering || _formLoading || WindowState == FormWindowState.Minimized) return;

			_isRendering = true;

			if (_videoElement.State?.Count > 0)
			{
				var image = _handler.GetImage(_videoElement.State.FirstOrDefault());
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
