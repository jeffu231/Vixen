using Vixen.Sys.Engine;
using Xunit;

namespace Vixen.Tests.Sys.Engine;

public class ExecutionSchedulerTests
{
	[Fact]
	public void Run_ActiveConsumer_ExecutesFirstFrameImmediatelyAndWaitsForSubsequentDeadlines()
	{
		// Arrange
		var timer = new FakeExecutionTimer();
		var metrics = new ExecutionFrameMetrics();
		var frameIds = new List<long>();
		ExecutionScheduler? scheduler = null;
		scheduler = new ExecutionScheduler(timer, TimeSpan.FromMilliseconds(25), () => true, frameId =>
		{
			frameIds.Add(frameId);
			if (frameId == 3)
			{
				scheduler!.Stop();
			}
		}, metrics);

		// Act
		scheduler.Run();

		// Assert
		Assert.Equal([1L, 2L, 3L], frameIds);
		Assert.Equal(3, metrics.RefreshCount);
		Assert.Equal(2, timer.WaitCount);
		Assert.Equal(0, metrics.MissedDeadlineCount);
		Assert.Contains(metrics.InstrumentationValues, value => value.Name == "Execution frame lateness");
	}

	[Fact]
	public async Task Run_NoActiveConsumers_DoesNotExecuteAFrame()
	{
		// Arrange
		var timer = new FakeExecutionTimer();
		var metrics = new ExecutionFrameMetrics();
		var activeConsumers = false;
		using var scheduler = new ExecutionScheduler(timer, TimeSpan.FromMilliseconds(25), () => activeConsumers, _ => { }, metrics);
		var runTask = Task.Run(scheduler.Run, TestContext.Current.CancellationToken);

		// Act
		await Task.Yield();
		scheduler.Stop();
		await runTask;

		// Assert
		Assert.Equal(0, metrics.RefreshCount);
		Assert.Equal(0, timer.WaitCount);
	}

	[Fact]
	public void Run_Overrun_RecordsMissedDeadlinesAndWaitsUntilNextFutureDeadline()
	{
		// Arrange
		var timer = new FakeExecutionTimer();
		var metrics = new ExecutionFrameMetrics();
		ExecutionScheduler? scheduler = null;
		scheduler = new ExecutionScheduler(timer, TimeSpan.FromMilliseconds(25), () => true, frameId =>
		{
			if (frameId == 1)
			{
				timer.Advance(TimeSpan.FromMilliseconds(80));
			}
			else
			{
				scheduler!.Stop();
			}
		}, metrics);

		// Act
		scheduler.Run();

		// Assert
		Assert.Equal(3, metrics.MissedDeadlineCount);
		Assert.Equal(1, timer.WaitCount);
		Assert.Equal(2, metrics.RefreshCount);
	}

	[Fact]
	public void Run_IntervalChanges_ResetsTheNextDeadlineAtTheFrameBoundary()
	{
		// Arrange
		var timer = new FakeExecutionTimer();
		var metrics = new ExecutionFrameMetrics();
		var interval = TimeSpan.FromMilliseconds(25);
		ExecutionScheduler? scheduler = null;
		scheduler = new ExecutionScheduler(timer, () => interval, () => true, frameId =>
		{
			if (frameId == 1)
			{
				interval = TimeSpan.FromMilliseconds(50);
			}
			else
			{
				scheduler!.Stop();
			}
		}, metrics);

		// Act
		scheduler.Run();

		// Assert
		Assert.Equal(2, metrics.RefreshCount);
		Assert.Equal(0, metrics.MissedDeadlineCount);
		Assert.Equal(49_500, timer.LastWaitDeadlineTimestamp);
	}
}
