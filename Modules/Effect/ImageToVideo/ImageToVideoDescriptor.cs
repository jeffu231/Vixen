using System;
using Vixen.Sys;
using Vixen.Module.Effect;
using Vixen.Sys.Attribute;
using VixenModules.Effect.Effect;

namespace VixenModules.Effect.ImageToVideo
{
	public class ImageToVideoDescriptor : EffectModuleDescriptorBase
	{
		private static Guid _typeId = new Guid("{0881132C-9D54-4741-ABD1-BD08197666AF}");

        public ImageToVideoDescriptor()
        {
            ModulePath = EffectName;
        }

        [ModuleDataPath]
        public static string ModulePath { get; set; }

        public override string EffectName
		{
			get { return "Image to Video"; }
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
			get { return typeof (ImageToVideo); }
		}

		public override Type ModuleDataClass
		{
			get { return typeof (ImageToVideoData); }
		}

        public override bool SupportsFiles => true;

        //Used when dragging files from Windows Explorer and will grab the appropiate file extensions to check.
        public override string[] SupportedFileExtensions => StandardMediaExtensions.ImageExtensions;

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
			get { return "Puts an Image file on a Video Element"; }
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