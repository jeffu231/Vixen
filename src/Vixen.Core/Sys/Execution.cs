using System.Diagnostics;
using System.Collections.Concurrent;
using NLog;
using Vixen.Instrumentation;
using Vixen.Sys.Instrumentation;
using Vixen.Sys.Managers;
using Vixen.Sys.State.Execution;
using Vixen.Sys.Engine;
using Vixen.Sys.Output;

namespace Vixen.Sys
{
	public class Execution
	{
		private static readonly Logger Logging = LogManager.GetCurrentClassLogger();
		internal static SystemClock SystemTime = new();
		private static ExecutionStateEngine _state;
		private static MillisecondsValue _executionUpdateTime;
		private static MillisecondsValue _executionSleepTime;
		private static MillisecondsValue _executionUpdateOutputDevicesTime;
		private static MillisecondsValue _executionUpdatePreviewsTime;
		private static RateValue _executionUpdateRate;
		private static Stopwatch _stopwatch;
		private static bool _lastUpdateClearedStates;
		private static Thread _executionThread;
		private static ExecutionScheduler _executionScheduler;
		private static readonly Lock ExecutionSchedulerSyncRoot = new();
		private static readonly Dictionary<Guid, int> ControllerFailureCounts = new();
		private static readonly ConcurrentQueue<OutputController> ControllersToStop = new();
		private static readonly double TicksPerMicrosecond = Stopwatch.Frequency / 1_000_000.0;
		
		/// <summary>
		/// Tick time length in [ms]
		/// </summary>
		public static readonly double TickLength = 1000.0 / Stopwatch.Frequency;

		public static void InitInstrumentation()
		{
			_executionUpdateTime = new MillisecondsValue("Execution update time");
			VixenSystem.Instrumentation.AddValue(_executionUpdateTime);
			_executionSleepTime = new MillisecondsValue("Execution sleep time");
			VixenSystem.Instrumentation.AddValue(_executionSleepTime);
			_executionUpdateOutputDevicesTime = new MillisecondsValue("Execution outputs update time");
			VixenSystem.Instrumentation.AddValue(_executionUpdateOutputDevicesTime);
			_executionUpdatePreviewsTime = new MillisecondsValue("Execution previews update time");
			VixenSystem.Instrumentation.AddValue(_executionUpdatePreviewsTime);

			_executionUpdateRate = new ExecutionEngineRefreshRateValue();
			VixenSystem.Instrumentation.AddValue(_executionUpdateRate);
			_stopwatch = Stopwatch.StartNew();
		}

		// These are system-level events.
		public static event EventHandler NodesChanged
		{
			add { NodeManager.NodesChanged += value; }
			remove { NodeManager.NodesChanged -= value; }
		}

		public static event EventHandler ExecutionStateChanged
		{
			add { _State.StateChanged += value; }
			remove { _State.StateChanged -= value; }
		}

		public static void OpenExecution()
		{
			_State.ToOpen();
		}

		public static void CloseExecution()
		{
			_State.ToClosed();
		}

		public static void OpenTest()
		{
			_State.ToTest();
		}

		public static void CloseTest()
		{
			_State.ToClosed();
		}

		internal static void Startup()
		{
			lock (ExecutionSchedulerSyncRoot)
			{
				if (_executionThread?.IsAlive == true)
				{
					return;
				}

				_executionScheduler?.Dispose();
				_executionScheduler = new ExecutionScheduler(new WindowsHighResolutionExecutionTimer(),
					() => VixenSystem.DefaultUpdateTimeSpan,
					HasActiveConsumers,
					ExecuteFrame,
					new ExecutionFrameMetrics());
				_executionScheduler.NotifyActiveConsumerStateChanged();
				_executionThread = new Thread(UpdateState) { Name = "Execution State Update", IsBackground = true, Priority = ThreadPriority.Normal };
				_executionThread.Start();
			}
			Logging.Info("Execution Startup");
		}

		internal static void Shutdown()
		{
			Thread executionThread;
			lock (ExecutionSchedulerSyncRoot)
			{
				_executionScheduler?.Stop();
				executionThread = _executionThread;
			}
			if (executionThread != null && executionThread != Thread.CurrentThread)
			{
				executionThread.Join();
			}
			lock (ExecutionSchedulerSyncRoot)
			{
				_executionScheduler?.Dispose();
				_executionScheduler = null;
				_executionThread = null;
			}
			Logging.Info("Execution shutdown");
		}

		private static ExecutionStateEngine _State
		{
			get { return _state ?? (_state = new ExecutionStateEngine()); }
		}

		public static string State
		{
			get { return _State.CurrentState.Name; }
		}

		public static bool IsOpen
		{
			get { return State == OpenState.StateName || State == OpeningState.StateName; }
		}

		public static bool IsClosed
		{
			get { return State == ClosedState.StateName || State == ClosingState.StateName; }
		}

		public static bool IsInTest
		{
			get { return State == TestOpeningState.StateName || State == TestOpenState.StateName; }
		}

		public static TimeSpan CurrentExecutionTime
		{
			get { return (SystemTime.IsRunning) ? SystemTime.Position : TimeSpan.Zero; }
		}

		public static string CurrentExecutionTimeString
		{
			get { return CurrentExecutionTime.ToString("m\\:ss\\.fff"); }
		}

		private static void UpdateState()
		{
			if (_stopwatch == null)
			{
				InitInstrumentation();
			}

			try
			{
				_executionScheduler?.Run();
			}
			catch (Exception exception)
			{
				Logging.Error(exception, "Execution scheduler terminated unexpectedly.");
			}
			finally
			{
				Logging.Info("Execution thread exiting");
			}
		}

		private static bool HasActiveConsumers()
		{
			using var controllers = VixenSystem.OutputControllers.AcquireActiveSnapshot();
			using var previews = VixenSystem.Previews.AcquireActiveSnapshot();
			return controllers.Devices.Length > 0 || previews.Devices.Length > 0;
		}

		private static void ExecuteFrame(long frameId)
		{
			while (ControllersToStop.TryDequeue(out var controllerToStop))
			{
				VixenSystem.OutputControllers.Stop(controllerToStop);
			}

			_stopwatch!.Restart();
			using var controllers = VixenSystem.OutputControllers.AcquireActiveSnapshot();
			using var previews = VixenSystem.Previews.AcquireActiveSnapshot();

				bool elementsAffected = VixenSystem.Contexts.Update();
				if (elementsAffected)
				{
					VixenSystem.Elements.Update();
					_lastUpdateClearedStates = false;
					if (controllers.Devices.Length > 0)
					{
						//Only update the filter chain if we have a controller running
						VixenSystem.Filters.Update();
					}
				}
				else if (!_lastUpdateClearedStates)
				{
					//No need to sample all the contexts as we were just told there are no elements effected.
					VixenSystem.Elements.ClearStates();
					_lastUpdateClearedStates = true;
					if (controllers.Devices.Length > 0)
					{
						//Only update the filter chain if we have a controller running
						VixenSystem.Filters.Update();
					}
				}

				UpdatePreviews(previews.Devices);
				UpdateOutputDevices(controllers.Devices);
				
				_executionUpdateTime.Set(_stopwatch.ElapsedMilliseconds);
				_executionUpdateRate.Increment();
				
		}

		private static void Sleep(double microseconds)
		{
			
			long start = Stopwatch.GetTimestamp();
			// Calculate the exact number of ticks we need to wait
			long durationTicks = (long)(microseconds * TicksPerMicrosecond);
			long targetTicks = start + durationTicks;

			// Hybrid approach to save CPU for longer waits.
			// On Windows, the default system timer resolution is often ~15.6ms (64Hz).
			// If the sleep is large enough (e.g., > 20ms), we can safely sleep 
			// part of the way and then spin for the rest.
			if (microseconds >= 20_000)
			{
				// Sleep for the duration minus a safe margin (approx 15-20ms) to allow 
				// the OS scheduler enough time to wake us up before the target.
				int msToSleep = (int)(microseconds / 1000) - 16;
				if (msToSleep > 0)
				{
					Thread.Sleep(msToSleep);
				}
			}

			// Busy-wait (spin) loop for the remaining precision
			while (Stopwatch.GetTimestamp() < targetTicks)
			{
				// Thread.SpinWait yields to the processor (using PAUSE instruction on x86)
				// preventing 100% CPU load on the core while maintaining high responsiveness.
				Thread.SpinWait(1);
			}
		
		}

		private static double ElapsedHiRes(Stopwatch stopwatch)
		{
			return stopwatch.ElapsedTicks * TickLength;
		}

		private static void UpdateOutputDevices(OutputController[] outputControllers)
		{
			var start = _stopwatch.ElapsedMilliseconds;
			var updateTasks = outputControllers.Select(outputController => Task.Run(() => UpdateController(outputController))).ToArray();
			var failedControllers = Task.WhenAll(updateTasks).GetAwaiter().GetResult().Where(controller => controller != null).ToArray();
			foreach (var controller in outputControllers.Except(failedControllers))
			{
				ControllerFailureCounts.Remove(controller.Id);
			}
			foreach (var controller in failedControllers)
			{
				var failures = ControllerFailureCounts.GetValueOrDefault(controller.Id) + 1;
				ControllerFailureCounts[controller.Id] = failures;
				if (failures >= 5)
				{
					Logging.Error("Controller {0} failed five consecutive frames and will be stopped.", controller.Name);
					ControllerFailureCounts.Remove(controller.Id);
					ControllersToStop.Enqueue(controller);
				}
			}
			_executionUpdateOutputDevicesTime.Set(_stopwatch.ElapsedMilliseconds - start);
		}

		private static OutputController UpdateController(OutputController outputController)
		{
			try
			{
				outputController.UpdateFrame();
				return null;
			}
			catch (Exception exception)
			{
				Logging.Error(exception, "Controller {0} failed while consuming the frame.", outputController.Name);
				return outputController;
			}
		}

		private static void UpdatePreviews(OutputPreview[] previews)
		{
			var start = _stopwatch.ElapsedMilliseconds;
			
			foreach (var preview in previews)
			{
				//We can update synchronous as this will just get posted to the UI thread anyway
				preview.Update();
			}

			_executionUpdatePreviewsTime.Set(_stopwatch.ElapsedMilliseconds - start);
		}
	}
}
