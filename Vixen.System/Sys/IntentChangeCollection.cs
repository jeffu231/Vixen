using System.Collections.Generic;
using System.Linq;

namespace Vixen.Sys
{
	public class IntentChangeCollection
	{
		public IntentChangeCollection(IEnumerable<IIntentState> addedIntents, IEnumerable<IIntentState> removedIntents)
		{
			if (addedIntents == null) addedIntents = Enumerable.Empty<IIntentState>();
			if (removedIntents == null) removedIntents = Enumerable.Empty<IIntentState>();

			AddedIntents = addedIntents.ToArray();
			RemovedIntents = removedIntents.ToArray();
		}

		public IIntentState[] AddedIntents { get; private set; }

		public IIntentState[] RemovedIntents { get; private set; }
	}
}