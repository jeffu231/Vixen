using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using NLog;

namespace Vixen.Sys.Engine
{
	internal sealed class WindowsHighResolutionExecutionTimer : IExecutionTimer
	{
		private const uint CreateWaitableTimerHighResolution = 0x00000002;
		private const uint TimerModifyStateAndSynchronize = 0x00100002;
		private static readonly Logger Logging = LogManager.GetCurrentClassLogger();
		private readonly EventWaitHandle _wakeEvent = new(false, EventResetMode.AutoReset);
		private readonly EventWaitHandle _timerEvent;
		private readonly bool _usesTimePeriodFallback;
		private bool _isDisposed;

		public WindowsHighResolutionExecutionTimer(bool forceFallback = false)
		{
			if (!forceFallback && OperatingSystem.IsWindows())
			{
				var timerHandle = CreateWaitableTimerEx(IntPtr.Zero, null, CreateWaitableTimerHighResolution, TimerModifyStateAndSynchronize);
				if (!timerHandle.IsInvalid)
				{
					_timerEvent = new EventWaitHandle(false, EventResetMode.AutoReset);
					_timerEvent.SafeWaitHandle = timerHandle;
					return;
				}

				Logging.Warn("Unable to create a high-resolution execution timer. Win32 error: {0}. Using timer-resolution fallback.", Marshal.GetLastWin32Error());
			}

			_timerEvent = new EventWaitHandle(false, EventResetMode.AutoReset);
			_usesTimePeriodFallback = OperatingSystem.IsWindows();
			if (_usesTimePeriodFallback)
			{
				var result = TimeBeginPeriod(1);
				if (result != 0)
				{
					Logging.Warn("Unable to enable one-millisecond timer resolution. Result: {0}.", result);
					_usesTimePeriodFallback = false;
				}
				else
				{
					Logging.Info("Execution scheduler is using the timer-resolution fallback.");
				}
			}
		}

		public long Timestamp => Stopwatch.GetTimestamp();

		public long Frequency => Stopwatch.Frequency;

		public ExecutionTimerWaitResult WaitUntil(long deadlineTimestamp, CancellationToken cancellationToken)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return ExecutionTimerWaitResult.Cancelled;
			}

			if (Timestamp >= deadlineTimestamp)
			{
				return ExecutionTimerWaitResult.DeadlineReached;
			}

			using var cancellationRegistration = cancellationToken.Register(Wake);
			if (_usesTimePeriodFallback || !OperatingSystem.IsWindows())
			{
				return WaitUsingFallback(deadlineTimestamp, cancellationToken);
			}

			var remainingTicks = Math.Max(1, deadlineTimestamp - Timestamp);
			var dueTime = -checked((long)Math.Ceiling(remainingTicks * (10_000_000d / Frequency)));
			if (!SetWaitableTimer(_timerEvent.SafeWaitHandle, ref dueTime, 0, IntPtr.Zero, IntPtr.Zero, false))
			{
				Logging.Warn("Unable to set the execution timer. Win32 error: {0}.", Marshal.GetLastWin32Error());
				return WaitUsingFallback(deadlineTimestamp, cancellationToken);
			}

			var result = WaitHandle.WaitAny([_timerEvent, _wakeEvent]);
			if (cancellationToken.IsCancellationRequested)
			{
				return ExecutionTimerWaitResult.Cancelled;
			}

			return result == 0 ? ExecutionTimerWaitResult.DeadlineReached : ExecutionTimerWaitResult.Woken;
		}

		public void Wake()
		{
			if (!_isDisposed)
			{
				_wakeEvent.Set();
			}
		}

		public void Dispose()
		{
			if (_isDisposed)
			{
				return;
			}

			_wakeEvent.Set();
			_timerEvent.Dispose();
			_wakeEvent.Dispose();
			if (_usesTimePeriodFallback)
			{
				var result = TimeEndPeriod(1);
				if (result != 0)
				{
					Logging.Warn("Unable to restore timer resolution. Result: {0}.", result);
				}
			}
			_isDisposed = true;
		}

		private ExecutionTimerWaitResult WaitUsingFallback(long deadlineTimestamp, CancellationToken cancellationToken)
		{
			var remainingTicks = deadlineTimestamp - Timestamp;
			if (remainingTicks <= 0)
			{
				return ExecutionTimerWaitResult.DeadlineReached;
			}

			var milliseconds = Math.Max(1, (int)Math.Ceiling(remainingTicks * (1000d / Frequency)));
			var result = _wakeEvent.WaitOne(milliseconds);
			if (cancellationToken.IsCancellationRequested)
			{
				return ExecutionTimerWaitResult.Cancelled;
			}

			return result ? ExecutionTimerWaitResult.Woken : ExecutionTimerWaitResult.DeadlineReached;
		}

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern SafeWaitHandle CreateWaitableTimerEx(IntPtr timerAttributes, string timerName, uint flags, uint desiredAccess);

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool SetWaitableTimer(SafeWaitHandle timerHandle, ref long dueTime, int period, IntPtr completionRoutine,
			IntPtr argumentToCompletionRoutine, [MarshalAs(UnmanagedType.Bool)] bool resume);

		[DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
		private static extern uint TimeBeginPeriod(uint milliseconds);

		[DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
		private static extern uint TimeEndPeriod(uint milliseconds);
	}
}
