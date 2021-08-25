using System.Runtime.Serialization;
using Vixen.Module;

namespace VixenModules.Property.Tilt
{
	[DataContract]
	public class TiltData : ModuleDataModelBase
	{
		public override IModuleDataModel Clone()
		{
			TiltData newInstance = new TiltData();
			return newInstance;
		}

		[DataMember]
		public int Range { get; set; }

		
	}
}