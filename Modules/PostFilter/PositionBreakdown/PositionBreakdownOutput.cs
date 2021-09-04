using System.Collections.Generic;
using System.Linq;
using Vixen.Data.Flow;
using Vixen.Data.Value;
using Vixen.Sys;

namespace VixenModules.OutputFilter.PositionBreakdown
{
	internal class PositionBreakdownOutput : IDataFlowOutput<IntentsDataFlowData>
	{
		private readonly List<IIntentState> _states;
		private readonly PositionBreakdownFilter _filter;
		private readonly IntentsDataFlowData _intentData;

		public PositionBreakdownOutput(PositionType type)
		{
			_filter = new PositionBreakdownFilter(type);
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