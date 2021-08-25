using System.Runtime.Serialization;
using Vixen.Module;

namespace VixenModules.Property.Pan
{
	[DataContract]
	public class PanData : ModuleDataModelBase
	{
		public override IModuleDataModel Clone()
		{
			PanData newInstance = new PanData();
			return newInstance;
		}

		[DataMember]
		public int Range { get; set; }

	}
}