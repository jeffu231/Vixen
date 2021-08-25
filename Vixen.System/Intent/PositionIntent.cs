using System;
using Vixen.Data.Value;

namespace Vixen.Intent
{
	public class PositionIntent : NonSegmentedLinearIntent<PositionValue>
	{
		public PositionIntent(PositionValue startValue, PositionValue endValue, TimeSpan timeSpan)
			: base(startValue, endValue, timeSpan)
		{
			if (startValue.PositionType != endValue.PositionType)
				throw new ArgumentException("Position value types must be equal");
		}

	}
}