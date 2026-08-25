using Vixen.Sys.Engine;
using Vixen.Sys.Output;
using Xunit;

namespace Vixen.Tests.Sys.Engine;

public class ExecutionFrameMetricsTests
{
	[Fact]
	public void InstrumentationValues_RecordSchedulerCountsAndMillisecondDurations()
	{
		// Arrange
		var metrics = new ExecutionFrameMetrics();

		// Act
		metrics.RecordFrameStart(100, 125);
		metrics.RecordControllerFailureCount(2);
		metrics.RecordActiveConsumerCounts(3, 4);
		metrics.RecordPreviewMetrics(5, 250);

		// Assert
		Assert.Contains(metrics.InstrumentationValues, value => value.Name == "Execution active controllers" && value.Value == 3);
		Assert.Contains(metrics.InstrumentationValues, value => value.Name == "Execution active previews" && value.Value == 4);
		Assert.Contains(metrics.InstrumentationValues, value => value.Name == "Execution controller failures" && value.Value == 2);
		Assert.Contains(metrics.InstrumentationValues, value => value.Name == "Execution preview coalesced frames" && value.Value == 5);
		Assert.Contains(metrics.InstrumentationValues, value => value.Name == "Execution frame lateness" && value.FormattedValue.EndsWith(" ms"));
	}

	[Fact]
	public void OutputDeviceCompatibilityMembers_AreObsolete()
	{
		// Arrange
		var updateInterval = typeof(IOutputDevice).GetProperty("UpdateInterval");
		var updateSignaler = typeof(IOutputDevice).GetProperty("UpdateSignaler");

		// Act
		var updateIntervalObsolete = updateInterval!.GetCustomAttributes(typeof(ObsoleteAttribute), inherit: false);
		var updateSignalerObsolete = updateSignaler!.GetCustomAttributes(typeof(ObsoleteAttribute), inherit: false);

		// Assert
		Assert.Single(updateIntervalObsolete);
		Assert.Single(updateSignalerObsolete);
	}
}
