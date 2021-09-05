using System.Collections.Generic;

namespace Vixen.Sys.Output
{
	internal class IntentOutputStates
	{
		private Dictionary<IntentOutput, IIntentState[]> _outputStates;

		public IntentOutputStates()
		{
			_outputStates = new Dictionary<IntentOutput, IIntentState[]>();
		}

		public IIntentState[] GetOutputCurrentState(IntentOutput output)
		{
			IIntentState[] outputCurrentState;
			_outputStates.TryGetValue(output, out outputCurrentState);
			return outputCurrentState;
		}

		public void SetOutputCurrentState(IntentOutput output, IIntentState[] state)
		{
			_outputStates[output] = state;
		}
	}
}