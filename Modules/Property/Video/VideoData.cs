using System.Runtime.Serialization;
using Vixen.Module;

namespace VixenModules.Property.Video
{
	[DataContract]
	public class VideoData : ModuleDataModelBase
	{
		[DataMember]
		public int Width { get; set; }

		[DataMember]
		public int Height { get; set; }

		public override IModuleDataModel Clone()
		{
			return (VideoData) MemberwiseClone();
		}
	}
}