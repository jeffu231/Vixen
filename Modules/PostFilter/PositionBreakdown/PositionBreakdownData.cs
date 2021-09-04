using System.Runtime.Serialization;
using Vixen.Module;

namespace VixenModules.OutputFilter.PositionBreakdown
{
	public class PositionBreakdownData : ModuleDataModelBase
	{
		public override IModuleDataModel Clone()
		{
			PositionBreakdownData newInstance = (PositionBreakdownData)MemberwiseClone();
			return newInstance;
		}


		[DataMember] 
		public bool CanPan { get; set; } = true;

		[DataMember] 
		public bool CanTilt { get; set; } = true;

	}
}