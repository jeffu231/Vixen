using System.Runtime.Serialization;
using VixenModules.App.Curves;
using VixenModules.Effect.Effect;

namespace VixenModules.Effect.SetPosition
{
	[DataContract]
	public class SetPositionData : EffectTypeModuleData
	{
		public SetPositionData()
		{
			Pan = new Curve(CurveType.RampUp);
			Tilt = new Curve(CurveType.RampDown);
		}
		[DataMember]
		public Curve Pan { get; set; }

		[DataMember]
		public Curve Tilt { get; set; }

		protected override EffectTypeModuleData CreateInstanceForClone()
		{
			SetPositionData result = new SetPositionData
			{
				Pan = new Curve(Pan),
				Tilt = new Curve(Tilt)
			};
			return result;
		}
	}
}