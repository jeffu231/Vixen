using System.Runtime.Serialization;
using Vixen.Module;

namespace VixenModules.Preview.VideoPreview
{
	[DataContract]
	public class VideoPreviewData : ModuleDataModelBase
	{
		public override IModuleDataModel Clone()
		{
			VideoPreviewData result = new VideoPreviewData
			                          	{
			                          		Width = 1024,
			                          		Height = 800
			                          	};
			return result;
		}

		[DataMember]
		public int Top { get; set; }

		[DataMember]
		public int Left { get; set; }

		[DataMember]
		public int Width { get; set; }

		[DataMember]
		public int Height { get; set; }

		[DataMember]
		public bool UseOpenGL { get; set; } = true;
	}
}