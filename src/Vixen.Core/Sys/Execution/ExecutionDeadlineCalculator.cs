namespace Vixen.Sys.Engine
{
	internal sealed class ExecutionDeadlineCalculator
	{
		private long _intervalTicks;
		private long _nextDeadlineTimestamp;
		private bool _isInitialized;

		public long IntervalTicks => _intervalTicks;

		public long NextDeadlineTimestamp => _nextDeadlineTimestamp;

		public void Reset(long timestamp, long intervalTicks)
		{
			if (intervalTicks <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(intervalTicks), intervalTicks, "The execution interval must be positive.");
			}

			_intervalTicks = intervalTicks;
			_nextDeadlineTimestamp = timestamp;
			_isInitialized = true;
		}

		public ExecutionDeadlineResult CompleteFrame(long timestamp)
		{
			if (!_isInitialized)
			{
				throw new InvalidOperationException("The execution deadline calculator must be initialized before completing a frame.");
			}

			var nextDeadlineTimestamp = checked(_nextDeadlineTimestamp + _intervalTicks);
			var missedDeadlineCount = 0L;
			if (timestamp >= nextDeadlineTimestamp)
			{
				missedDeadlineCount = ((timestamp - nextDeadlineTimestamp) / _intervalTicks) + 1;
				nextDeadlineTimestamp = checked(nextDeadlineTimestamp + (missedDeadlineCount * _intervalTicks));
			}

			_nextDeadlineTimestamp = nextDeadlineTimestamp;
			return new ExecutionDeadlineResult(nextDeadlineTimestamp, missedDeadlineCount);
		}

		public static long ToStopwatchTicks(TimeSpan interval, long stopwatchFrequency)
		{
			if (interval <= TimeSpan.Zero)
			{
				throw new ArgumentOutOfRangeException(nameof(interval), interval, "The execution interval must be positive.");
			}
			if (stopwatchFrequency <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(stopwatchFrequency), stopwatchFrequency, "The stopwatch frequency must be positive.");
			}

			return checked((long)Math.Round(interval.TotalSeconds * stopwatchFrequency, MidpointRounding.AwayFromZero));
		}
	}
}
