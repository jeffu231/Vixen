namespace Vixen.Sys.Engine
{
	internal readonly record struct ExecutionDeadlineResult(long NextDeadlineTimestamp, long MissedDeadlineCount);
}
