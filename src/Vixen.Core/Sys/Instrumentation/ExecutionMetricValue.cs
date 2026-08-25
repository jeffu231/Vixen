using Vixen.Instrumentation;

namespace Vixen.Sys.Instrumentation
{
	internal sealed class ExecutionMetricValue : InstrumentationValue
	{
		private readonly Func<double> _getValue;

		public ExecutionMetricValue(string name, Func<double> getValue)
			: base(name)
		{
			ArgumentNullException.ThrowIfNull(getValue);
			_getValue = getValue;
		}

		protected override double _GetValue()
		{
			return _getValue();
		}

		protected override string _GetFormattedValue()
		{
			return _getValue().ToString("0.###");
		}

		public override void Reset()
		{
		}
	}
}
