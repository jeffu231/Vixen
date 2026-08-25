using Vixen.Sys.Managers;
using Xunit;

namespace Vixen.Tests.Sys.Engine;

public class OutputDeviceExecutionTests
{
	[Fact]
	public void StartAll_PublishesDeviceOnlyAfterStartCompletes()
	{
		// Arrange
		var execution = new OutputDeviceExecution<TestOutputDevice>();
		var device = new TestOutputDevice();

		// Act
		execution.StartAll([device]);
		using var snapshot = execution.AcquireActiveSnapshot();

		// Assert
		Assert.True(device.IsRunning);
		Assert.Equal([device], snapshot.Devices);
	}

	[Fact]
	public async Task Pause_WaitsForLeasedFrameBeforeCallingDevicePause()
	{
		// Arrange
		var execution = new OutputDeviceExecution<TestOutputDevice>();
		var device = new TestOutputDevice();
		execution.StartAll([device]);
		var snapshot = execution.AcquireActiveSnapshot();

		// Act
		var pauseTask = Task.Run(() => execution.Pause(device), TestContext.Current.CancellationToken);
		await Task.Yield();

		// Assert
		Assert.False(pauseTask.IsCompleted);
		snapshot.Dispose();
		await pauseTask;
		Assert.True(device.IsPaused);
		using var activeSnapshot = execution.AcquireActiveSnapshot();
		Assert.Empty(activeSnapshot.Devices);
		execution.Resume(device);
		using var resumedSnapshot = execution.AcquireActiveSnapshot();
		Assert.Equal([device], resumedSnapshot.Devices);
	}
}
