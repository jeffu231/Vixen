using Vixen.Sys.Output;

namespace Vixen.Sys.Managers
{
	internal sealed class OutputDeviceExecution<T> : IOutputDeviceExecution<T>
		where T : class, IOutputDevice
	{
		private static readonly NLog.Logger Logging = NLog.LogManager.GetCurrentClassLogger();
		private readonly object _syncRoot = new();
		private readonly Dictionary<Guid, T> _outputDevices = new();
		private readonly Dictionary<Guid, int> _inFlightFrameCounts = new();
		private T[] _activeDevices = [];

		public ExecutionState ExecutionState { get; private set; } = ExecutionState.Stopped;

		public void Start(T outputDevice) => StartDevice(outputDevice);
		public void Stop(T outputDevice) => StopDevice(outputDevice);
		public void Pause(T outputDevice) => PauseDevice(outputDevice);
		public void Resume(T outputDevice) => ResumeDevice(outputDevice);
		public void StartAll() => StartAll(GetStartedDevices());
		public void StopAll() => StopAll(GetStartedDevices());
		public void PauseAll() => PauseAll(GetStartedDevices());
		public void ResumeAll() => ResumeAll(GetStartedDevices());

		public void StartAll(IEnumerable<T> outputDevices)
		{
			if (ExecutionState != ExecutionState.Stopped) return;
			ExecutionState = ExecutionState.Starting;
			ApplyToDevices(outputDevices, StartDevice);
			ExecutionState = ExecutionState.Started;
		}

		public void StopAll(IEnumerable<T> outputDevices)
		{
			if (ExecutionState is not (ExecutionState.Started or ExecutionState.Paused)) return;
			ExecutionState = ExecutionState.Stopping;
			ApplyToDevices(outputDevices, StopDevice);
			ExecutionState = ExecutionState.Stopped;
		}

		public void PauseAll(IEnumerable<T> outputDevices)
		{
			if (ExecutionState != ExecutionState.Started) return;
			ApplyToDevices(outputDevices, PauseDevice);
			ExecutionState = ExecutionState.Paused;
		}

		public void ResumeAll(IEnumerable<T> outputDevices)
		{
			if (ExecutionState != ExecutionState.Paused) return;
			ApplyToDevices(outputDevices, ResumeDevice);
			ExecutionState = ExecutionState.Started;
		}

		public OutputDeviceSnapshot<T> AcquireActiveSnapshot()
		{
			lock (_syncRoot)
			{
				foreach (var outputDevice in _activeDevices)
				{
					_inFlightFrameCounts[outputDevice.Id] = _inFlightFrameCounts.GetValueOrDefault(outputDevice.Id) + 1;
				}
				return new OutputDeviceSnapshot<T>(_activeDevices, ReleaseSnapshot);
			}
		}

		private void StartDevice(T outputDevice)
		{
			if (!CanStart(outputDevice)) return;
			try
			{
				outputDevice.Start();
				lock (_syncRoot)
				{
					_outputDevices[outputDevice.Id] = outputDevice;
					PublishActiveDevices();
				}
				Execution.NotifyActiveConsumerStateChanged();
			}
			catch (Exception exception)
			{
				Logging.Error(exception, "Error starting device {0}", outputDevice.Name);
			}
		}

		private void StopDevice(T outputDevice)
		{
			if (!CanStop(outputDevice)) return;
			try
			{
				UnpublishAndWait(outputDevice, removeDevice: true);
				outputDevice.Stop();
			}
			catch (Exception exception)
			{
				Logging.Error(exception, "Error stopping device {0}", outputDevice.Name);
			}
		}

		private void PauseDevice(T outputDevice)
		{
			if (!CanPause(outputDevice)) return;
			try
			{
				UnpublishAndWait(outputDevice, removeDevice: false);
				outputDevice.Pause();
			}
			catch (Exception exception)
			{
				Logging.Error(exception, "Error pausing device {0}", outputDevice.Name);
			}
		}

		private void ResumeDevice(T outputDevice)
		{
			if (!CanResume(outputDevice)) return;
			try
			{
				outputDevice.Resume();
				lock (_syncRoot)
				{
					if (outputDevice.IsRunning && !outputDevice.IsPaused) PublishActiveDevices();
				}
				Execution.NotifyActiveConsumerStateChanged();
			}
			catch (Exception exception)
			{
				Logging.Error(exception, "Error resuming device {0}", outputDevice.Name);
			}
		}

		private bool CanStart(T outputDevice) => !outputDevice.IsRunning && IsInRunningState;
		private static bool CanStop(T outputDevice) => outputDevice.IsRunning;
		private static bool CanPause(T outputDevice) => outputDevice.IsRunning && !outputDevice.IsPaused;
		private bool CanResume(T outputDevice) => outputDevice.IsRunning && outputDevice.IsPaused && IsInRunningState;
		private bool IsInRunningState => ExecutionState is ExecutionState.Starting or ExecutionState.Started or ExecutionState.Paused;

		private T[] GetStartedDevices()
		{
			lock (_syncRoot) return _outputDevices.Values.ToArray();
		}

		private static void ApplyToDevices(IEnumerable<T> outputDevices, Action<T> action)
		{
			foreach (var outputDevice in outputDevices.ToArray()) action(outputDevice);
		}

		private void UnpublishAndWait(T outputDevice, bool removeDevice)
		{
			lock (_syncRoot)
			{
				_activeDevices = _activeDevices.Where(device => device.Id != outputDevice.Id).ToArray();
			}

			Execution.NotifyActiveConsumerStateChanged();

			lock (_syncRoot)
			{
				while (_inFlightFrameCounts.GetValueOrDefault(outputDevice.Id) > 0) Monitor.Wait(_syncRoot);
				if (removeDevice) _outputDevices.Remove(outputDevice.Id);
			}
		}

		private void PublishActiveDevices()
		{
			_activeDevices = _outputDevices.Values.Where(device => device.IsRunning && !device.IsPaused).ToArray();
		}

		private void ReleaseSnapshot(T[] outputDevices)
		{
			lock (_syncRoot)
			{
				foreach (var outputDevice in outputDevices)
				{
					var count = _inFlightFrameCounts.GetValueOrDefault(outputDevice.Id) - 1;
					if (count <= 0) _inFlightFrameCounts.Remove(outputDevice.Id);
					else _inFlightFrameCounts[outputDevice.Id] = count;
				}
				Monitor.PulseAll(_syncRoot);
			}
		}
	}
}
