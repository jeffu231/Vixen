using System.Diagnostics;

namespace Vixen.Sys.Engine
{
	internal sealed class ExecutionScheduler : IDisposable
	{
		internal const int FinalSpinBudgetMicroseconds = 500;

		private readonly IExecutionTimer _timer;
		private readonly Func<TimeSpan> _getInterval;
		private readonly Action<long> _executeFrame;
		private readonly Func<bool> _hasActiveConsumers;
		private readonly ExecutionDeadlineCalculator _deadlineCalculator = new();
		private readonly CancellationTokenSource _cancellationTokenSource = new();
		private readonly ManualResetEventSlim _activeConsumerEvent = new(false);
		private readonly ManualResetEventSlim _quiescenceReleasedEvent = new(true);
		private readonly ManualResetEventSlim _frameCompletedEvent = new(true);
		private readonly Lock _quiescenceSyncRoot = new();
		private int _quiescenceCount;
		private bool _isDisposed;

		public ExecutionScheduler(IExecutionTimer timer, TimeSpan interval, Func<bool> hasActiveConsumers, Action<long> executeFrame,
			ExecutionFrameMetrics metrics)
			: this(timer, () => interval, hasActiveConsumers, executeFrame, metrics)
		{
		}

		public ExecutionScheduler(IExecutionTimer timer, Func<TimeSpan> getInterval, Func<bool> hasActiveConsumers, Action<long> executeFrame,
			ExecutionFrameMetrics metrics)
		{
			ArgumentNullException.ThrowIfNull(timer);
			ArgumentNullException.ThrowIfNull(getInterval);
			ArgumentNullException.ThrowIfNull(hasActiveConsumers);
			ArgumentNullException.ThrowIfNull(executeFrame);
			ArgumentNullException.ThrowIfNull(metrics);

			_timer = timer;
			_getInterval = getInterval;
			_hasActiveConsumers = hasActiveConsumers;
			_executeFrame = executeFrame;
			Metrics = metrics;
		}

		public ExecutionFrameMetrics Metrics { get; }

		/// <summary>
		/// Gets a value indicating whether ordinary frames are currently prevented from starting.
		/// </summary>
		internal bool IsQuiesced
		{
			get
			{
				lock (_quiescenceSyncRoot) return _quiescenceCount > 0;
			}
		}

		public void NotifyActiveConsumerStateChanged()
		{
			if (_hasActiveConsumers())
			{
				_activeConsumerEvent.Set();
			}
			else
			{
				_activeConsumerEvent.Reset();
			}

			_timer.Wake();
		}

		/// <summary>
		/// Prevents ordinary frames from starting until the returned lease is disposed.
		/// </summary>
		/// <returns>A lease that releases one quiescence request.</returns>
		public ExecutionQuiesceLease Quiesce()
		{
			lock (_quiescenceSyncRoot)
			{
				_quiescenceCount++;
				_quiescenceReleasedEvent.Reset();
			}

			_timer.Wake();
			_frameCompletedEvent.Wait(_cancellationTokenSource.Token);
			return new ExecutionQuiesceLease(ReleaseQuiescence);
		}

		public void Run()
		{
			var cancellationToken = _cancellationTokenSource.Token;
			var frameId = 0L;
			var wasIdle = true;
			var previousFrameStartTimestamp = 0L;
			var intervalTicks = GetIntervalTicks();

			while (!cancellationToken.IsCancellationRequested)
			{
				if (IsQuiesced)
				{
					wasIdle = true;
					WaitForQuiescenceRelease(cancellationToken);
					continue;
				}

				if (!_hasActiveConsumers())
				{
					wasIdle = true;
					WaitForActiveConsumers(cancellationToken);
					continue;
				}

				var frameStartTimestamp = _timer.Timestamp;
				if (wasIdle)
				{
					_deadlineCalculator.Reset(frameStartTimestamp, intervalTicks);
					wasIdle = false;
				}

				var scheduledDeadlineTimestamp = _deadlineCalculator.NextDeadlineTimestamp;
				Metrics.RecordFrameStart(scheduledDeadlineTimestamp, frameStartTimestamp);
				if (previousFrameStartTimestamp != 0)
				{
					Metrics.RecordIntervalError(frameStartTimestamp - previousFrameStartTimestamp, intervalTicks);
				}
				previousFrameStartTimestamp = frameStartTimestamp;

				if (!TryBeginFrame())
				{
					wasIdle = true;
					continue;
				}

				try
				{
					var updateStartTimestamp = _timer.Timestamp;
					_executeFrame(++frameId);
					Metrics.RecordFrameUpdateDuration(_timer.Timestamp - updateStartTimestamp);
				}
				finally
				{
					EndFrame();
				}

				var deadlineResult = _deadlineCalculator.CompleteFrame(_timer.Timestamp);
				Metrics.RecordMissedDeadlines(deadlineResult.MissedDeadlineCount);
				var updatedIntervalTicks = GetIntervalTicks();
				if (updatedIntervalTicks != intervalTicks)
				{
					intervalTicks = updatedIntervalTicks;
					_deadlineCalculator.Reset(checked(_timer.Timestamp + intervalTicks), intervalTicks);
					deadlineResult = new ExecutionDeadlineResult(_deadlineCalculator.NextDeadlineTimestamp, deadlineResult.MissedDeadlineCount);
				}
				WaitForDeadline(deadlineResult.NextDeadlineTimestamp, cancellationToken);
			}
		}

		public void Stop()
		{
			_cancellationTokenSource.Cancel();
			_activeConsumerEvent.Set();
			_quiescenceReleasedEvent.Set();
			_frameCompletedEvent.Set();
			_timer.Wake();
		}

		public void Dispose()
		{
			if (_isDisposed)
			{
				return;
			}

			Stop();
			_activeConsumerEvent.Dispose();
			_quiescenceReleasedEvent.Dispose();
			_frameCompletedEvent.Dispose();
			_cancellationTokenSource.Dispose();
			_timer.Dispose();
			_isDisposed = true;
		}

		private void WaitForActiveConsumers(CancellationToken cancellationToken)
		{
			try
			{
				_activeConsumerEvent.Wait(cancellationToken);
			}
			catch (OperationCanceledException)
			{
			}
		}

		private bool TryBeginFrame()
		{
			lock (_quiescenceSyncRoot)
			{
				if (_quiescenceCount > 0) return false;
				_frameCompletedEvent.Reset();
				return true;
			}
		}

		private void EndFrame()
		{
			lock (_quiescenceSyncRoot)
			{
				_frameCompletedEvent.Set();
			}
		}

		private void ReleaseQuiescence()
		{
			lock (_quiescenceSyncRoot)
			{
				if (_quiescenceCount == 0) return;
				if (--_quiescenceCount != 0) return;
				_quiescenceReleasedEvent.Set();
			}
			_timer.Wake();
		}

		private void WaitForQuiescenceRelease(CancellationToken cancellationToken)
		{
			try
			{
				_quiescenceReleasedEvent.Wait(cancellationToken);
			}
			catch (OperationCanceledException)
			{
			}
		}

		private long GetIntervalTicks()
		{
			return ExecutionDeadlineCalculator.ToStopwatchTicks(_getInterval(), _timer.Frequency);
		}

		private void WaitForDeadline(long deadlineTimestamp, CancellationToken cancellationToken)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return;
			}

			var spinBudgetTicks = Math.Max(1, (_timer.Frequency * FinalSpinBudgetMicroseconds) / 1_000_000);
			var timerDeadlineTimestamp = deadlineTimestamp - spinBudgetTicks;
			var waitStartTimestamp = _timer.Timestamp;
			if (timerDeadlineTimestamp > waitStartTimestamp)
			{
				var waitResult = _timer.WaitUntil(timerDeadlineTimestamp, cancellationToken);
				Metrics.RecordSleepDuration(_timer.Timestamp - waitStartTimestamp);
				if (waitResult != ExecutionTimerWaitResult.DeadlineReached)
				{
					return;
				}
			}

			var spinStartTimestamp = _timer.Timestamp;
			var spinner = new SpinWait();
			while (!cancellationToken.IsCancellationRequested && _timer.Timestamp < deadlineTimestamp)
			{
				spinner.SpinOnce();
			}
			Metrics.RecordFinalSpinDuration(_timer.Timestamp - spinStartTimestamp);
		}
	}
}
