using Vixen.Instrumentation;
using Vixen.Sys.Instrumentation;

namespace Vixen.Sys.Engine
{
	internal sealed class ExecutionFrameMetrics
	{
		private readonly IReadOnlyList<IInstrumentationValue> _instrumentationValues;

		public ExecutionFrameMetrics()
		{
			_instrumentationValues =
			[
				new ExecutionMetricValue("Execution scheduled deadline", () => ScheduledDeadlineTimestamp),
				new ExecutionMetricValue("Execution frame lateness", () => FrameStartLatenessTicks),
				new ExecutionMetricValue("Execution frame update duration", () => FrameUpdateDurationTicks),
				new ExecutionMetricValue("Execution output barrier duration", () => OutputBarrierDurationTicks),
				new ExecutionMetricValue("Execution sleep duration", () => SleepDurationTicks),
				new ExecutionMetricValue("Execution final spin duration", () => FinalSpinDurationTicks),
				new ExecutionMetricValue("Execution interval error", () => IntervalErrorTicks),
				new ExecutionMetricValue("Execution missed deadlines", () => MissedDeadlineCount),
				new ExecutionMetricValue("Execution refresh count", () => RefreshCount)
			];
		}

		public IReadOnlyList<IInstrumentationValue> InstrumentationValues => _instrumentationValues;

		public long ScheduledDeadlineTimestamp { get; private set; }
		public long FrameStartLatenessTicks { get; private set; }
		public long FrameUpdateDurationTicks { get; private set; }
		public long OutputBarrierDurationTicks { get; private set; }
		public long SleepDurationTicks { get; private set; }
		public long FinalSpinDurationTicks { get; private set; }
		public long IntervalErrorTicks { get; private set; }
		public long MissedDeadlineCount { get; private set; }
		public long RefreshCount { get; private set; }

		public void RecordFrameStart(long scheduledDeadlineTimestamp, long actualStartTimestamp)
		{
			ScheduledDeadlineTimestamp = scheduledDeadlineTimestamp;
			FrameStartLatenessTicks = Math.Max(0, actualStartTimestamp - scheduledDeadlineTimestamp);
			RefreshCount++;
		}

		public void RecordFrameUpdateDuration(long durationTicks)
		{
			FrameUpdateDurationTicks = Math.Max(0, durationTicks);
		}

		public void RecordOutputBarrierDuration(long durationTicks)
		{
			OutputBarrierDurationTicks = Math.Max(0, durationTicks);
		}

		public void RecordSleepDuration(long durationTicks)
		{
			SleepDurationTicks = Math.Max(0, durationTicks);
		}

		public void RecordFinalSpinDuration(long durationTicks)
		{
			FinalSpinDurationTicks = Math.Max(0, durationTicks);
		}

		public void RecordIntervalError(long actualIntervalTicks, long expectedIntervalTicks)
		{
			IntervalErrorTicks = actualIntervalTicks - expectedIntervalTicks;
		}

		public void RecordMissedDeadlines(long missedDeadlineCount)
		{
			MissedDeadlineCount += Math.Max(0, missedDeadlineCount);
		}
	}
}
