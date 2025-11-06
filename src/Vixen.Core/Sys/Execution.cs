using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Vixen.Execution;
using Vixen.Execution.Context;
using Vixen.Instrumentation;
using Vixen.Sys.Instrumentation;
using Vixen.Sys.Managers;
using Vixen.Sys.Output;
using Vixen.Sys.State.Execution;
using Vixen.Utility;

namespace Vixen.Sys
{
	public class Execution
	{
		internal static SystemClock SystemTime = new SystemClock();
		private static ExecutionStateEngine _state;
		//private static ControllerUpdateAdjudicator _updateAdjudicator;
		private static MillisecondsValue _executionUpdateTime;
		private static MillisecondsValue _executionSleepTime;
		private static MillisecondsValue _systemDeniedUpdateTime;
		private static MillisecondsValue _systemDeniedBlockTime;
		private static RateValue _executionUpdateRate;
		private static Stopwatch _stopwatch;
		private static long lastMs = 0;
		private static bool lastUpdateClearedStates = false;
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
			//_systemDeniedUpdateTime = new MillisecondsValue("System denied update");
			//VixenSystem.Instrumentation.AddValue(_systemDeniedUpdateTime);
			//_systemDeniedBlockTime = new MillisecondsValue("System denied block");
			//VixenSystem.Instrumentation.AddValue(_systemDeniedBlockTime);

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
		}

		internal static void Shutdown()
		{
		}

		//private static ControllerUpdateAdjudicator _UpdateAdjudicator
		//{
		//	get
		//	{
		//		//*** user-configurable threshold value
		//		return _updateAdjudicator ?? (_updateAdjudicator = new ControllerUpdateAdjudicator(10));
		//	}
		//}

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

		private static ConcurrentDictionary<string, TimeSpan> lastSnapshots = new ConcurrentDictionary<string, TimeSpan>();
		private static readonly Object LockObject = new Object();

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
				
				lock (LockObject)
				{
					bool elementsAffected = VixenSystem.Contexts.Update();
					if (elementsAffected)
					{
						VixenSystem.Elements.Update();
						lastUpdateClearedStates = false;
						if (VixenSystem.OutputControllers.Any(x => x.IsRunning))
						{
							//Only update the filter chain if we have a controller running
							VixenSystem.Filters.Update();
							updateOutputTask = UpdateOutputDevicesAsync();
						}

						updatePreviewTask = UpdatePreviewsAsync();
					}
					else if (!lastUpdateClearedStates)
					{
						//No need to sample all the contexts as we were just told there are no elements effected.
						VixenSystem.Elements.ClearStates();
						lastUpdateClearedStates = true;
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
				}

				var sleepStart = _stopwatch.ElapsedMilliseconds;
				Sleep();
				_executionSleepTime.Set(_stopwatch.ElapsedMilliseconds - sleepStart);
			}

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

		//public static ConcurrentDictionary<string, TimeSpan> UpdateState( out bool allowed)
		//{
		//	if (_stopwatch == null)
		//		InitInstrumentation();
		//	long nowMs = _stopwatch.ElapsedMilliseconds;
		//	lock (LockObject) {
		//		long lockMs = _stopwatch.ElapsedMilliseconds - nowMs;
		//		bool allowUpdate = _UpdateAdjudicator.PetitionForUpdate();
		//		if (allowUpdate) {
		//			bool elementsAffected = VixenSystem.Contexts.Update();
		//			if (elementsAffected)
		//			{
		//				VixenSystem.Elements.Update();
		//				lastUpdateClearedStates = false;
		//				if (VixenSystem.OutputControllers.Any(x => x.IsRunning))
		//				{
		//					//Only update the filter chain if we have a controller running
		//					VixenSystem.Filters.Update();
		//				}
		//			}
		//			else if(!lastUpdateClearedStates)
		//			{
		//				//No need to sample all the contexts as we were just told there are no elements effected.
		//				VixenSystem.Elements.ClearStates();
		//				lastUpdateClearedStates = true;
		//				if (VixenSystem.OutputControllers.Any(x => x.IsRunning))
		//				{
		//					//Only update the filter chain if we have a controller running
		//					VixenSystem.Filters.Update();
		//				}
		//			}

		//			_executionSleepTime.Set( lockMs);
		//			_executionUpdateTime.Set(_stopwatch.ElapsedMilliseconds - nowMs - lockMs);
		//		}
		//		else {
		//			_systemDeniedBlockTime.Set(lockMs);
		//			_systemDeniedUpdateTime.Set(_stopwatch.ElapsedMilliseconds - nowMs - lockMs);
		//		}
		//		lastMs = nowMs;
		//		allowed = allowUpdate;
		//		return lastSnapshots;
		//	}
		//}
	}
}