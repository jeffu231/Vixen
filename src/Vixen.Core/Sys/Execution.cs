using System.Diagnostics;
using NLog;
using Vixen.Instrumentation;
using Vixen.Sys.Instrumentation;
using Vixen.Sys.Managers;
using Vixen.Sys.State.Execution;

namespace Vixen.Sys
{
	public class Execution
	{
		private static readonly Logger Logging = LogManager.GetCurrentClassLogger();
		internal static SystemClock SystemTime = new SystemClock();
		private static ExecutionStateEngine _state;
		private static MillisecondsValue _executionUpdateTime;
		private static MillisecondsValue _executionSleepTime;
		private static RateValue _executionUpdateRate;
		private static Stopwatch _stopwatch;
		private static bool _lastUpdateClearedStates = false;
		private static Thread _executionThread;
		
		/// <summary>
		/// Tick time length in [ms]
		/// </summary>
		public static readonly double TickLength = 1000f / Stopwatch.Frequency;

		public static void InitInstrumentation()
		{
			_executionUpdateTime = new MillisecondsValue("Execution update time");
			VixenSystem.Instrumentation.AddValue(_executionUpdateTime);
			_executionSleepTime = new MillisecondsValue("Execution sleep time");
			VixenSystem.Instrumentation.AddValue(_executionSleepTime);
			
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
			if (_executionThread == null)
			{
				_executionThread = new Thread(UpdateState) { Name = "Execution State Update", IsBackground = true, Priority = ThreadPriority.Highest };
			}
			_executionThread.Start();
			Logging.Info("Execution Startup");
		}

		internal static void Shutdown()
		{
			_executionThread = null;
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

			Task updateOutputTask = Task.CompletedTask;
			Task updatePreviewTask = Task.CompletedTask;
			while (!IsClosed)
			{
				_stopwatch!.Restart();
				
				bool elementsAffected = VixenSystem.Contexts.Update();
				if (elementsAffected)
				{
					VixenSystem.Elements.Update();
					_lastUpdateClearedStates = false;
					if (VixenSystem.OutputControllers.Any(x => x.IsRunning))
					{
						//Only update the filter chain if we have a controller running
						VixenSystem.Filters.Update();
						updateOutputTask = UpdateOutputDevicesAsync();
					}

					updatePreviewTask = UpdatePreviewsAsync();
				}
				else if (!_lastUpdateClearedStates)
				{
					//No need to sample all the contexts as we were just told there are no elements effected.
					VixenSystem.Elements.ClearStates();
					_lastUpdateClearedStates = true;
					if (VixenSystem.OutputControllers.Any(x => x.IsRunning))
					{
						//Only update the filter chain if we have a controller running
						VixenSystem.Filters.Update();
						updateOutputTask = UpdateOutputDevicesAsync();
					}

					updatePreviewTask = UpdatePreviewsAsync();
				}

				Task.WhenAll(updatePreviewTask, updateOutputTask).Wait();

				_executionUpdateTime.Set(_stopwatch.ElapsedMilliseconds);
				_executionUpdateRate.Increment();
				
				var sleepStart = _stopwatch.ElapsedMilliseconds;
				Sleep();
				_executionSleepTime.Set(_stopwatch.ElapsedMilliseconds - sleepStart);
			}
			
			Logging.Info("Execution thread exiting");
		}

		private static void Sleep()
		{
			while (true)
			{
				var elapsed = ElapsedHiRes(_stopwatch);
				double diff = VixenSystem.DefaultUpdateInterval - elapsed;
				if (diff <= 0f)
					break;

				if (diff < 1f)
					Thread.SpinWait(10);
				else if (diff < 5f)
					Thread.SpinWait(100);
				else if (diff < 15f)
					Thread.Sleep(1);
				else
					Thread.Sleep(10);

				if (IsClosed)
				{
					return;
				}
			}
		}

		private static double ElapsedHiRes(Stopwatch stopwatch)
		{
			return stopwatch.ElapsedTicks * TickLength;
		}

		private static readonly List<Task> UpdateOutputTasks = new();
		private static async Task UpdateOutputDevicesAsync()
		{
			UpdateOutputTasks.Clear();
			foreach (var outputController in VixenSystem.OutputControllers.Where(c => c.IsRunning))
			{
				var task = outputController.UpdateAsync();
				UpdateOutputTasks.Add(task);
			}

			await Task.WhenAll(UpdateOutputTasks);
		}

		private static readonly List<Task> UpdatePreviewTasks = new();
		private static async Task UpdatePreviewsAsync()
		{
			foreach (var preview in VixenSystem.Previews.Where(p => p.IsRunning))
			{
				var task = preview.UpdateAsync();
				UpdatePreviewTasks.Add(task);
			}

			await Task.WhenAll(UpdatePreviewTasks);
		}
	}
}