using System.Collections.Concurrent;
using Vixen.Sys.Output;

namespace Vixen.Sys.Managers
{
	internal class OutputDeviceExecution<T> : IOutputDeviceExecution<T>
		where T : class, IOutputDevice
	{
		private static readonly NLog.Logger Logging = NLog.LogManager.GetCurrentClassLogger();
		private readonly ConcurrentDictionary<Guid, T> _outputDevices = new();

		public void Start(T outputDevice)
		{
			_Start(outputDevice);
		}

		public void Stop(T outputDevice)
		{
			_Stop(outputDevice);
		}

		public void Pause(T outputDevice)
		{
			_Pause(outputDevice);
		}

		public void Resume(T outputDevice)
		{
			_Resume(outputDevice);
		}

		public void StartAll()
		{
			_StartAll(_AllDevices());
		}

		public void StartAll(IEnumerable<T> outputDevices)
		{
			_StartAll(outputDevices);
		}

		public void StopAll()
		{
			_StopAll(_AllDevices());
		}

		public void StopAll(IEnumerable<T> outputDevices)
		{
			_StopAll(outputDevices);
		}

		public void PauseAll()
		{
			_PauseAll(_AllDevices());
		}

		public void PauseAll(IEnumerable<T> outputDevices)
		{
			_PauseAll(outputDevices);
		}

		public void ResumeAll()
		{
			_ResumeAll(_AllDevices());
		}

		public void ResumeAll(IEnumerable<T> outputDevices)
		{
			_ResumeAll(outputDevices);
		}

		public ExecutionState ExecutionState { get; private set; } = ExecutionState.Stopped;

		private void _StartAll(IEnumerable<T> outputDevices)
		{
			// For now, doing them serially in the UI thread.  When there are multiple 
			// modules, there seems to be a problem with what's invoked and when
			// (including one controller being invoked from multiple threads while
			//  other controllers not being invoked...sounded like a closure issue
			//  initially, but I can't figure it out).
			if (ExecutionState == ExecutionState.Stopped) {
				ExecutionState = ExecutionState.Starting;
				_DeviceAction(outputDevices, _Start);
				ExecutionState = ExecutionState.Started;
			}
		}

		private void _StopAll(IEnumerable<T> outputDevices)
		{
			if (ExecutionState == ExecutionState.Started) {
				ExecutionState = ExecutionState.Stopping;
				_DeviceAction(outputDevices, _Stop);
				ExecutionState = ExecutionState.Stopped;
			}
		}

		private void _PauseAll(IEnumerable<T> outputDevices)
		{
			if (ExecutionState == ExecutionState.Started) {
				_DeviceAction(outputDevices, _Pause);
				ExecutionState = ExecutionState.Paused;
			}
		}

		private void _ResumeAll(IEnumerable<T> outputDevices)
		{
			if (ExecutionState == ExecutionState.Paused) {
				_DeviceAction(outputDevices, _Resume);
				ExecutionState = ExecutionState.Started;
			}
		}

		private void _Start(T outputDevice)
		{
			if (_CanStart(outputDevice)) {
				try
				{
					_outputDevices[outputDevice.Id] = outputDevice;
					outputDevice.Start();
				}
				catch (Exception ex) {
					Logging.Error(ex, "Error starting device " + outputDevice.Name);
				}
			}
		}

		private void _Stop(T outputDevice)
		{
			if (_CanStop(outputDevice)) {
				try
				{
					_outputDevices.Remove(outputDevice.Id, out _);
					outputDevice.Stop();
				}
				catch (Exception ex) {
					Logging.Error(ex, "Error trying to stop device " + outputDevice.Name);
				}
			}
		}

		private void _Pause(T outputDevice)
		{
			if (_CanPause(outputDevice)) {
				try {
					outputDevice.Pause();
				}
				catch (Exception ex) {
					Logging.Error(ex, "Error trying to pause device " + outputDevice.Name);
				}
			}
		}

		private void _Resume(T outputDevice)
		{
			if (_CanResume(outputDevice)) {
				try {
					outputDevice.Resume();
				}
				catch (Exception ex) {
					Logging.Error(ex, "Error trying to resume device " + outputDevice.Name);
				}
			}
		}

		private bool _CanStart(T outputDevice)
		{
			return !outputDevice.IsRunning && InRunningState;
		}

		private bool _CanStop(T outputDevice)
		{
			return outputDevice.IsRunning;
		}

		private bool _CanPause(T outputDevice)
		{
			return outputDevice.IsRunning;
		}

		private bool _CanResume(T outputDevice)
		{
			return outputDevice.IsRunning && outputDevice.IsPaused && InRunningState;
		}

		private bool InRunningState
		{
			get
			{
				return ExecutionState == ExecutionState.Starting || ExecutionState == ExecutionState.Started ||
				       ExecutionState == ExecutionState.Paused;
			}
		}

		private IEnumerable<T> _AllDevices()
		{
			return _outputDevices.Values;
		}

		private void _DeviceAction(IEnumerable<T> outputDevices, Action<T> action)
		{
			foreach (T outputDevice in outputDevices.ToArray()) {
				action(outputDevice);
			}
		}
	}
}