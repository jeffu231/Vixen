using System.Runtime.Serialization;
using VixenModules.Effect.Effect;

namespace VixenModules.Effect.ImageToVideo
{
	[DataContract]
	internal class ImageToVideoData : EffectTypeModuleData
	{
        [DataMember]
        public string FileName { get; set; }

        public ImageToVideoData()
		{
            FileName = "";
        }

		protected override EffectTypeModuleData CreateInstanceForClone()
		{
			ImageToVideoData result = new ImageToVideoData();
            result.FileName = FileName;
			return result;
		}
	}
}