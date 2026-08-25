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
				new ExecutionMillisecondsValue("Execution frame lateness", () => FrameStartLatenessTicks),
				new ExecutionMillisecondsValue("Execution frame update duration", () => FrameUpdateDurationTicks),
				new ExecutionMillisecondsValue("Execution output barrier duration", () => OutputBarrierDurationTicks),
				new ExecutionMillisecondsValue("Execution sleep duration", () => SleepDurationTicks),
				new ExecutionMillisecondsValue("Execution final spin duration", () => FinalSpinDurationTicks),
				new ExecutionMillisecondsValue("Execution interval error", () => IntervalErrorTicks),
				new ExecutionMillisecondsValue("Execution preview frame age", () => PreviewFrameAgeTicks),
				new ExecutionCountValue("Execution missed deadlines", () => MissedDeadlineCount),
				new ExecutionCountValue("Execution refresh count", () => RefreshCount),
				new ExecutionCountValue("Execution active controllers", () => ActiveControllerCount),
				new ExecutionCountValue("Execution active previews", () => ActivePreviewCount),
				new ExecutionCountValue("Execution controller failures", () => ControllerFailureCount),
				new ExecutionCountValue("Execution preview coalesced frames", () => PreviewCoalescedFrameCount),
				new ExecutionCountValue("Execution timer fallback active", () => UsesTimerFallback ? 1 : 0)
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
		public long ActiveControllerCount { get; private set; }
		public long ActivePreviewCount { get; private set; }
		public long ControllerFailureCount { get; private set; }
		public long PreviewCoalescedFrameCount { get; private set; }
		public long PreviewFrameAgeTicks { get; private set; }
		public bool UsesTimerFallback { get; private set; }

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

		public void RecordActiveConsumerCounts(int controllerCount, int previewCount)
		{
			ActiveControllerCount = Math.Max(0, controllerCount);
			ActivePreviewCount = Math.Max(0, previewCount);
		}

		public void RecordControllerFailureCount(long controllerFailureCount)
		{
			ControllerFailureCount = Math.Max(0, controllerFailureCount);
		}

		public void RecordPreviewMetrics(long coalescedFrameCount, long frameAgeTicks)
		{
			PreviewCoalescedFrameCount = Math.Max(0, coalescedFrameCount);
			PreviewFrameAgeTicks = Math.Max(0, frameAgeTicks);
		}

		public void RecordTimerFallback(bool usesTimerFallback)
		{
			UsesTimerFallback = usesTimerFallback;
		}
	}
}
