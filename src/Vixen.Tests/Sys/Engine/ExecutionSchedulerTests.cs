using Vixen.Sys.Engine;
using ExecutionFacade = Vixen.Sys.Execution;
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

	[Fact]
	public async Task Quiesce_FrameInProgress_WaitsForCompletionAndPreventsAnotherFrame()
	{
		// Arrange
		var timer = new FakeExecutionTimer();
		var metrics = new ExecutionFrameMetrics();
		using var frameStarted = new ManualResetEventSlim();
		using var releaseFrame = new ManualResetEventSlim();
		using var scheduler = new ExecutionScheduler(timer, TimeSpan.FromMilliseconds(25), () => true, _ =>
		{
			frameStarted.Set();
			releaseFrame.Wait(TestContext.Current.CancellationToken);
		}, metrics);
		var runTask = Task.Run(scheduler.Run, TestContext.Current.CancellationToken);
		frameStarted.Wait(TestContext.Current.CancellationToken);
		Assert.True(frameStarted.IsSet);

		// Act
		var quiesceTask = Task.Run(scheduler.Quiesce, TestContext.Current.CancellationToken);
		await Task.Yield();

		// Assert
		Assert.False(quiesceTask.IsCompleted);
		releaseFrame.Set();
		using var lease = await quiesceTask;
		Assert.Equal(1, metrics.RefreshCount);

		// Cleanup
		scheduler.Stop();
		await runTask;
	}

	[Fact]
	public async Task Quiesce_OutputDeviceLifecycle_WaitsUntilTheLeaseIsReleased()
	{
		// Arrange
		using var lifecycleAttempted = new ManualResetEventSlim();
		using var lifecycleEntered = new ManualResetEventSlim();
		using var lease = ExecutionFacade.Quiesce();
		var lifecycleTask = Task.Run(() =>
		{
			lifecycleAttempted.Set();
			using var lifecycle = ExecutionFacade.EnterOutputDeviceLifecycle();
			lifecycleEntered.Set();
		}, TestContext.Current.CancellationToken);
		lifecycleAttempted.Wait(TestContext.Current.CancellationToken);
		Assert.True(lifecycleAttempted.IsSet);

		// Assert
		Assert.False(lifecycleEntered.Wait(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken));
		await lifecycleTask;
		Assert.True(lifecycleEntered.IsSet);
	}
}
