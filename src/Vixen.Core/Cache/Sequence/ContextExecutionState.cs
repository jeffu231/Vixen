using Vixen.Execution;

namespace Vixen.Cache.Sequence
{
	internal sealed record ContextExecutionState(IContext Context, bool IsRunning, bool IsPaused);
}
