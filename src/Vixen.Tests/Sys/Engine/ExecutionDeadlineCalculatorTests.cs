using Vixen.Sys.Engine;
using Xunit;

namespace Vixen.Tests.Sys.Engine;

public class ExecutionDeadlineCalculatorTests
{
	[Fact]
	public void CompleteFrame_OnTime_KeepsNextDeadlineAnchoredToOriginalSchedule()
	{
		// Arrange
		var calculator = new ExecutionDeadlineCalculator();
		calculator.Reset(timestamp: 100, intervalTicks: 25);

		// Act
		var result = calculator.CompleteFrame(timestamp: 110);

		// Assert
		Assert.Equal(125, result.NextDeadlineTimestamp);
		Assert.Equal(0, result.MissedDeadlineCount);
	}

	[Fact]
	public void CompleteFrame_AfterOverrun_SkipsMissedDeadlinesWithoutSchedulingCatchUpFrames()
	{
		// Arrange
		var calculator = new ExecutionDeadlineCalculator();
		calculator.Reset(timestamp: 100, intervalTicks: 25);

		// Act
		var result = calculator.CompleteFrame(timestamp: 180);

		// Assert
		Assert.Equal(200, result.NextDeadlineTimestamp);
		Assert.Equal(3, result.MissedDeadlineCount);
	}

	[Fact]
	public void Reset_AfterIdle_StartsFreshScheduleWithoutMissedDeadlines()
	{
		// Arrange
		var calculator = new ExecutionDeadlineCalculator();
		calculator.Reset(timestamp: 100, intervalTicks: 25);
		calculator.CompleteFrame(timestamp: 125);

		// Act
		calculator.Reset(timestamp: 10_000, intervalTicks: 25);
		var result = calculator.CompleteFrame(timestamp: 10_000);

		// Assert
		Assert.Equal(10_025, result.NextDeadlineTimestamp);
		Assert.Equal(0, result.MissedDeadlineCount);
	}

	[Fact]
	public void Reset_WithChangedInterval_AppliesNewIntervalAtFrameBoundary()
	{
		// Arrange
		var calculator = new ExecutionDeadlineCalculator();
		calculator.Reset(timestamp: 100, intervalTicks: 25);
		calculator.CompleteFrame(timestamp: 125);

		// Act
		calculator.Reset(timestamp: 125, intervalTicks: 50);
		var result = calculator.CompleteFrame(timestamp: 125);

		// Assert
		Assert.Equal(175, result.NextDeadlineTimestamp);
		Assert.Equal(0, result.MissedDeadlineCount);
	}

	[Fact]
	public void ToStopwatchTicks_RoundsAwayFromZero()
	{
		// Act
		var ticks = ExecutionDeadlineCalculator.ToStopwatchTicks(TimeSpan.FromMilliseconds(2.5), stopwatchFrequency: 1_000);

		// Assert
		Assert.Equal(3, ticks);
	}
}
