using Vixen.Sys.Output;

namespace Vixen.Tests.Sys.Engine;

internal sealed class TestOutputDevice : IOutputDevice
{
	public Guid Id { get; } = Guid.NewGuid();
	public Guid ModuleId { get; } = Guid.NewGuid();
	public string Name { get; set; } = "Test output";
	public bool IsRunning { get; private set; }
	public bool IsPaused { get; private set; }
	public bool HasSetup => false;
	public int UpdateInterval { get; set; } = 25;
	public IOutputDeviceUpdateSignaler UpdateSignaler => null!;

	public bool Setup() => true;
	public void Update() { }
	public Task UpdateAsync() => Task.CompletedTask;
	public void UpdateCommands() { }
	public void Start() => IsRunning = true;
	public void Stop()
	{
		IsRunning = false;
		IsPaused = false;
	}
	public void Pause() => IsPaused = true;
	public void Resume() => IsPaused = false;
}
