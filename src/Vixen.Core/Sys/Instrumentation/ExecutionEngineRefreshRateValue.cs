using Vixen.Instrumentation;

namespace Vixen.Sys.Instrumentation
{
	internal class ExecutionEngineRefreshRateValue() : RateValue("Execution refresh rate")
	{
		protected override string _GetFormattedValue()
		{
			return _GetValue().ToString("0.00 Hz");
		}
	}
}
