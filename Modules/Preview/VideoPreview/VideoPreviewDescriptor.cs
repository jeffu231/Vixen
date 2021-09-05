using System;
using Vixen.Module.Preview;
using Vixen.Sys.Attribute;
using VixenModules.Preview.VixenPreview;

namespace VixenModules.Preview.VideoPreview
{
	public class VideoPreviewDescriptor : PreviewModuleDescriptorBase
	{
		static VideoPreviewDescriptor()
		{
			ModulePath = "VideoPreview";
		}

		private static Guid _typeId = new Guid("{FF73BE21-B9F1-46ED-AFB1-C2B4D515B237}");

		[ModuleDataPath]
		public static string ModulePath { get; set; }

		public override Guid TypeId
		{
			get { return _typeId; }
		}

		public override Type ModuleClass
		{
			get { return typeof (VideoPreviewModuleInstance); }
		}

		public override Type ModuleDataClass
		{
			get { return typeof (VideoPreviewData); }
		}

		public override string Author
		{
			get { return "Jeff Uchitjil"; }

		}

		public override string TypeName
		{
			get { return "Video Preview"; }
		}

		public override string Description
		{
			get { return "Video Preview"; }
		}

		public override string Version
		{
			get { return "1.0.0"; }
		}

	}
}