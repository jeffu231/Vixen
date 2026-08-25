using Vixen.Sys.Engine;

namespace Vixen.Tests.Sys.Engine;

internal sealed class FakeExecutionTimer(long frequency = 1_000_000) : IExecutionTimer
{
	private bool _isWoken;

	public long Timestamp { get; private set; }

	public long Frequency { get; } = frequency;

	public int WaitCount { get; private set; }

	public long LastWaitDeadlineTimestamp { get; private set; }

	public ExecutionTimerWaitResult WaitUntil(long deadlineTimestamp, CancellationToken cancellationToken)
	{
		WaitCount++;
		LastWaitDeadlineTimestamp = deadlineTimestamp;
		if (cancellationToken.IsCancellationRequested)
		{
			return ExecutionTimerWaitResult.Cancelled;
		}
		if (_isWoken)
		{
			_isWoken = false;
			return ExecutionTimerWaitResult.Woken;
		}

		Timestamp = Math.Max(Timestamp, deadlineTimestamp + Frequency / 1_000);
		return ExecutionTimerWaitResult.DeadlineReached;
	}

	public void Advance(TimeSpan duration)
	{
		Timestamp += ExecutionDeadlineCalculator.ToStopwatchTicks(duration, Frequency);
	}

	public void Wake()
	{
		_isWoken = true;
	}

	public void Dispose()
	{
	}
}
