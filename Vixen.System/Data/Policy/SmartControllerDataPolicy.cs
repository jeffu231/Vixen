using System.Collections.Generic;
using System.Linq;
using Vixen.Data.Flow;
using Vixen.Sys;
using Vixen.Sys.Dispatch;

namespace Vixen.Data.Policy
{
	internal class SmartControllerDataPolicy : DataFlowDataDispatch
	{
		public IIntentState[] OutputCurrentState;

		public override void Handle(IntentsDataFlowData obj)
		{
			IntentChangeCollection intentChanges = null;
			var newState = obj.Value.ToArray();

			if (_OutputHasStateToCompare)
			{
				if (_OutputStateDiffersFrom(newState))
				{
					IEnumerable<IIntentState> addedIntents = newState.Except(OutputCurrentState);
					IEnumerable<IIntentState> removedIntents = OutputCurrentState.Except(newState);
					intentChanges = new IntentChangeCollection(addedIntents, removedIntents);
				}
			}
			else
			{
				intentChanges = new IntentChangeCollection(newState, null);
			}

			OutputCurrentState = newState.ToArray();
			Result = intentChanges;
		}

		public override void Handle(CommandDataFlowData obj)
		{
			OutputCurrentState = null;
			Result = null;
		}

		public override void Handle(CommandsDataFlowData obj)
		{
			OutputCurrentState = null;
			Result = null;
		}

		public IntentChangeCollection Result { get; private set; }

		private bool _OutputHasStateToCompare
		{
			get { return OutputCurrentState != null; }
		}

		private bool _OutputStateDiffersFrom(IEnumerable<IIntentState> state)
		{
			//*** test the effectiveness of this
			return !OutputCurrentState.SequenceEqual(state);
		}
	}
}