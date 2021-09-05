using System;
using System.Diagnostics;
using System.Windows.Forms;
using Vixen.Sys;
using Vixen.Sys.Instrumentation;
using VixenModules.Preview.VideoPreview.WPF;
using VixenModules.Preview.VixenPreview;

namespace VixenModules.Preview.VideoPreview
{
	public partial class VideoPreviewModuleInstance
	{
		private IDisplayForm _displayForm;
		private static readonly NLog.Logger Logging = NLog.LogManager.GetCurrentClassLogger();
		private readonly MillisecondsValue _updateTimeValue = new MillisecondsValue("Update time for video preview");

		public VideoPreviewModuleInstance()
		{
			VixenSystem.Instrumentation.AddValue(_updateTimeValue);
		}

		public override Vixen.Module.IModuleDataModel ModuleData
		{
			get
			{
				if (base.ModuleData == null) {
					base.ModuleData = new VideoPreviewData();
					Logging.Warn("Access of null ModuleData. Creating new one. (Thread ID: " +
					                            System.Threading.Thread.CurrentThread.ManagedThreadId + ")");
				}
				return base.ModuleData;
			}
			set
			{
				base.ModuleData = value;
			}
		}

		protected override Form Initialize()
		{
			SetupPreviewForm();
			return (Form)_displayForm;
		}

		private readonly object _formLock = new object();
		private void SetupPreviewForm()
		{
			lock (_formLock) {
				try
				{
					_displayForm = new VideoPreviewWPFContainer(GetDataModel(), InstanceId);
				}
				catch (Exception ex)
				{

					Logging.Error(ex, "An error occured trying to create the Video Preview.");
				}
				
				_displayForm.DisplayName = Name;
				_displayForm.Setup();
			}
		}

		private String _name;
		public override string Name
		{
			get => _name;
			set
			{
				_name = value;
				if (_displayForm != null)
				{
					_displayForm.DisplayName = value;
				}
			}
		}

		private VideoPreviewData GetDataModel()
		{
			return ModuleData as VideoPreviewData;
		}

		public override bool Setup()
		{
			//_setupForm = new VixenPreviewSetup3();
			//var data = GetDataModel();
			//_setupForm.Data = data;
			
			//_setupForm.ShowDialog();

			//if (data.UseOpenGL && _displayForm?.GetType() != typeof(OpenGlPreviewForm))
			//{
			//	_displayForm?.Close();
			//	SetupPreviewForm();
			//}

			//if (_displayForm != null)
			//{
			//	_displayForm.Data = GetDataModel();
			//	_displayForm.Setup();
			//}

			return base.Setup();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (_displayForm != null)
					_displayForm.Close();
			}
			
			base.Dispose(disposing);
		}

		/// <inheritdoc />
		protected override void PlayerActivatedImpl()
		{
			
		}

		/// <inheritdoc />
		protected override void PlayerDeactivatedImpl()
		{
			
		}

		protected override void Update()
		{
			var sw = Stopwatch.StartNew();
			try {
				_displayForm.UpdatePreview();
			}
			catch (Exception e) {
				Logging.Error("Exception in preview update {0} - {1}", e.Message, e.StackTrace);
			}
			_updateTimeValue.Set(sw.ElapsedMilliseconds);
		}
	}
}