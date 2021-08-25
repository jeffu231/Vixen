using System;

namespace Vixen.Data.Value
{
	public struct PositionValue : IIntentDataType
	{
		public PositionValue(PositionType positionType, double position)
		{
			if (position < 0 || position > 1) throw new ArgumentOutOfRangeException(nameof(position));

			Position = position;
			PositionType = positionType;
		}

		/// <summary>
		/// Position value between 0 and 1.
		/// </summary>
		public double Position;

		public PositionType PositionType { get; private set; }

	}
}