using Vixen.Data.Value;
using Vixen.Intent;
using Vixen.Sys;
using Vixen.Sys.Dispatch;

namespace VixenModules.OutputFilter.PositionBreakdown
{
	/// <summary>
	/// This filter gets the intensity percent for a given state for non mixing colors
	/// </summary>
	internal class PositionBreakdownFilter : IntentStateDispatch
	{
		private IIntentState _intentValue = null;

		public PositionBreakdownFilter(PositionType type)
		{
			PositionType = type;
		}

		public PositionType PositionType { get; private set; }

		public IIntentState Filter(IIntentState intentValue)
		{
			_intentValue = null;
			intentValue.Dispatch(this);
			return _intentValue;
		}

		public override void Handle(IIntentState<PositionValue> obj)
		{
			if (obj.GetValue().PositionType == PositionType)
			{
				_intentValue = obj;
			}
		}
	}
}