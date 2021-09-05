using System;
using Vixen.Sys;
using Vixen.Module.Effect;
using Vixen.Sys.Attribute;
using VixenModules.Effect.Effect;

namespace VixenModules.Effect.VideoToVideo
{
	public class VideoToVideoDescriptor : EffectModuleDescriptorBase
	{
		private static Guid _typeId = new Guid("{91153E92-89DE-41A0-B550-856D605F97A3}");

        public VideoToVideoDescriptor()
        {
            ModulePath = EffectName;
        }

        [ModuleDataPath]
        public static string ModulePath { get; set; }

        public override string EffectName
		{
			get { return "Video to Video"; }
		}

		public override EffectGroups EffectGroup
		{
			get { return EffectGroups.Basic; }
		}

		public override Guid TypeId
		{
			get { return _typeId; }
		}

		public override Type ModuleClass
		{
			get { return typeof (VideoToVideo); }
		}

		public override Type ModuleDataClass
		{
			get { return typeof (VideoToVideoData); }
		}
        public override bool SupportsFiles => true;

        //Used when dragging files from Windows Explorer and will grab the appropiate file extensions to check.
        public override string[] SupportedFileExtensions => StandardMediaExtensions.VideoExtensions;

        public override string Author
		{
			get { return "Jon Chuchla"; }
		}

		public override string TypeName
		{
			get { return EffectName; }
		}

		public override string Description
		{
			get { return "Puts an Video file on a Video Element"; }
		}

		public override string Version
		{
			get { return "1.0"; }
		}

		public override ParameterSignature Parameters
		{
			get { return new ParameterSignature(); }
		}
	}
}