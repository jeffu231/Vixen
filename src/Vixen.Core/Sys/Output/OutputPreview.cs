using Vixen.Module.Preview;

namespace Vixen.Sys.Output
{
	/// <summary>
	/// In-memory preview device.
	/// </summary>
	public class OutputPreview : IPreviewDevice
	{
		private IHardware _executionControl;
		private IOutputModuleConsumer<IPreviewModuleInstance> _outputModuleConsumer;
		private int? _updateInterval;
		private Vixen.Sys.Engine.PreviewFrameMailbox _frameMailbox;

		internal OutputPreview(Guid id, string name, IHardware executionControl,
		                       IOutputModuleConsumer<IPreviewModuleInstance> outputModuleConsumer)
		{
			if (executionControl == null) throw new ArgumentNullException("executionControl");
			if (outputModuleConsumer == null) throw new ArgumentNullException("outputModuleConsumer");

			_executionControl = executionControl;
			_outputModuleConsumer = outputModuleConsumer;
			Id = id;
			Name = name;
		}

		public void UpdateCommands()
		{
			//Not needed
		}

		public void Update()
		{
			PreviewModule.UpdateState();
		}

		/// <summary>
		/// Publishes the latest execution-frame token without waiting for the preview UI to render.
		/// </summary>
		/// <remarks>
		/// Rendering reads the live element state when the preview UI callback runs. Intermediate frame tokens may be coalesced.
		/// </remarks>
		internal void PublishFrame(Vixen.Sys.Engine.PreviewFrameSnapshot snapshot)
		{
			FrameMailbox.Publish(snapshot);
		}

		public Guid Id { get; private set; }

		public string Name
		{
			get
			{
				return _outputModuleConsumer.Module.Name;
				
			}
			set
			{
				_outputModuleConsumer.Module.Name = value;
			} 
		}

		public Guid ModuleId
		{
			get { return _outputModuleConsumer.ModuleId; }
		}

		public Guid ModuleInstanceId
		{
			get { return _outputModuleConsumer.ModuleInstanceId; }
		}

		public int UpdateInterval
		{
			get { return (_updateInterval.HasValue) ? _updateInterval.Value : _outputModuleConsumer.UpdateInterval; }
			set { _updateInterval = value; }
		}

		public IOutputDeviceUpdateSignaler UpdateSignaler
		{
			get { return _outputModuleConsumer.UpdateSignaler; }
		}

		public void Start()
		{
			_executionControl.Start();
		}

		public void Stop()
		{
			_executionControl.Stop();
			_frameMailbox?.Invalidate();
		}

		public void Pause()
		{
			_executionControl.Pause();
			_frameMailbox?.Invalidate();
		}

		public void Resume()
		{
			_executionControl.Resume();
		}

		public bool IsRunning
		{
			get { return _executionControl.IsRunning; }
		}

		public bool IsPaused
		{
			get { return _executionControl.IsPaused; }
		}

		public bool HasSetup
		{
			get { return _outputModuleConsumer.HasSetup; }
		}

		public bool Setup()
		{
			return _outputModuleConsumer.Setup();
		}

		public override string ToString()
		{
			return Name;
		}

		public IPreview PreviewModule
		{
			get { return _outputModuleConsumer.Module; }
		}

		private Vixen.Sys.Engine.PreviewFrameMailbox FrameMailbox => _frameMailbox ??= CreateFrameMailbox();

		private Vixen.Sys.Engine.PreviewFrameMailbox CreateFrameMailbox()
		{
			if (_outputModuleConsumer.Module is not Vixen.Sys.Engine.IPreviewFrameRenderer renderer)
			{
				throw new InvalidOperationException($"Preview module '{Name}' does not support frame publication.");
			}

			return new Vixen.Sys.Engine.PreviewFrameMailbox(renderer.Post, renderer.Render);
		}
	}
}
