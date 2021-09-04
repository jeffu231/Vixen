using System.Runtime.Serialization;
using Vixen.Module;

namespace VixenModules.OutputFilter.PanTiltBreakdown
{
	public class PanTiltBreakdownData : ModuleDataModelBase
	{
		public override IModuleDataModel Clone()
		{
			PanTiltBreakdownData newInstance = (PanTiltBreakdownData)MemberwiseClone();
			return newInstance;
		}


		[DataMember] 
		public bool CanPan { get; set; } = true;

		[DataMember] 
		public bool CanTilt { get; set; } = true;

	}
}