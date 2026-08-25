using Vixen.Sys.Engine;
using Xunit;

namespace Vixen.Tests.Sys.Engine;

public class WindowsHighResolutionExecutionTimerTests
{
	[Fact]
	public void Constructor_DefaultPath_CanBeDisposed()
	{
		// Act
		using var timer = new WindowsHighResolutionExecutionTimer();

		// Assert
		Assert.True(timer.Frequency > 0);
	}

	[Fact]
	public void Constructor_ForcedFallback_CanBeDisposed()
	{
		// Act
		using var timer = new WindowsHighResolutionExecutionTimer(forceFallback: true);

		// Assert
		Assert.True(timer.Frequency > 0);
	}

	[Fact]
	public void WaitUntil_DeadlineInPast_ReturnsImmediately()
	{
		// Arrange
		using var timer = new WindowsHighResolutionExecutionTimer(forceFallback: true);

		// Act
		var result = timer.WaitUntil(timer.Timestamp - 1, CancellationToken.None);

		// Assert
		Assert.Equal(ExecutionTimerWaitResult.DeadlineReached, result);
	}

	[Fact]
	public async Task WaitUntil_CancellationDuringWait_ReturnsCancelled()
	{
		// Arrange
		using var timer = new WindowsHighResolutionExecutionTimer(forceFallback: true);
		using var cancellationTokenSource = new CancellationTokenSource();
		var waitTask = Task.Run(() => timer.WaitUntil(timer.Timestamp + timer.Frequency, cancellationTokenSource.Token),
			TestContext.Current.CancellationToken);

		// Act
		cancellationTokenSource.Cancel();
		var result = await waitTask;

		// Assert
		Assert.Equal(ExecutionTimerWaitResult.Cancelled, result);
	}
}
