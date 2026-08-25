namespace Vixen.Sys.Engine
{
	internal interface IExecutionTimer : IDisposable
	{
		long Timestamp { get; }
		long Frequency { get; }
		ExecutionTimerWaitResult WaitUntil(long deadlineTimestamp, CancellationToken cancellationToken);
		void Wake();
	}
}
