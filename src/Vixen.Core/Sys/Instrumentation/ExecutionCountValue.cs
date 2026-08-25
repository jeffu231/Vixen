using Vixen.Instrumentation;

namespace Vixen.Sys.Instrumentation
{
	internal sealed class ExecutionCountValue(string name, Func<long> getValue) : InstrumentationValue(name)
	{
		private readonly Func<long> _getValue = getValue ?? throw new ArgumentNullException(nameof(getValue));

		protected override double _GetValue() => _getValue();

		protected override string _GetFormattedValue() => _GetValue().ToString("0");

		public override void Reset()
		{
		}
	}
}
