using System.Runtime.Serialization;
using VixenModules.Effect.Effect;
using System.Drawing;

namespace VixenModules.Effect.VideoToVideo
{
	[DataContract]
	internal class VideoToVideoData : EffectTypeModuleData
	{
        public VideoToVideoData()
		{
            FileName = "";
            VideoSize = new Size(600, 400);
        }

        [DataMember]
        public string FileName { get; set; }

        [DataMember]
        public int VideoLength { get; set; }

        [DataMember]
        public Size VideoSize { get; set; }

        protected override EffectTypeModuleData CreateInstanceForClone()
        {
            VideoToVideoData result = new VideoToVideoData();
            result.FileName = FileName;
			return result;
		}
	}
}