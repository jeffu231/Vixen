using Vixen.Sys.Output;

namespace Vixen.Cache.Sequence
{
	internal sealed record OutputDeviceExecutionState(IOutputDevice Device, bool IsRunning, bool IsPaused);
}
