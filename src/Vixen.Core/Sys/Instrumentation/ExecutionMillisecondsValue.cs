using System.Diagnostics;
using Vixen.Instrumentation;

namespace Vixen.Sys.Instrumentation
{
	internal sealed class ExecutionMillisecondsValue(string name, Func<long> getTicks) : InstrumentationValue(name)
	{
		private readonly Func<long> _getTicks = getTicks ?? throw new ArgumentNullException(nameof(getTicks));

		protected override double _GetValue() => _getTicks() * 1000d / Stopwatch.Frequency;

		protected override string _GetFormattedValue() => $"{_GetValue():0.###} ms";

		public override void Reset()
		{
		}
	}
}
