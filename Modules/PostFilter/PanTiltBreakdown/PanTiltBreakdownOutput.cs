using System.Collections.Generic;
using System.Linq;
using Vixen.Data.Flow;
using Vixen.Sys;

namespace VixenModules.OutputFilter.PanTiltBreakdown
{
	internal class PanTiltBreakdownOutput : IDataFlowOutput<IntentsDataFlowData>
	{
		private readonly List<IIntentState> _states;
		private readonly PanTiltBreakdownFilter _filter;
		private readonly IntentsDataFlowData _intentData;

		public PanTiltBreakdownOutput(PositionType type)
		{
			_filter = new PanTiltBreakdownFilter(type);
			_states = new List<IIntentState>();
			_intentData = new IntentsDataFlowData(Enumerable.Empty<IIntentState>().ToList())
			{
				Value = _states
			};
		}

		public void ProcessInputData(IntentsDataFlowData data)
		{
			_states.Clear();
			foreach (var intentState in data.Value)
			{
				var state = _filter.Filter(intentState);
				if (state != null)
				{
					_states.Add(state);
				}
			}
		}

		IDataFlowData IDataFlowOutput.Data => Data;

		public string Name => _filter.PositionType.ToString();

		/// <inheritdoc />
		public IntentsDataFlowData Data => _intentData;
	}

}