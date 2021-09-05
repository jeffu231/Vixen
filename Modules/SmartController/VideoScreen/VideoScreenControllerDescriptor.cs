using System;
using Vixen.Module.SmartController;

namespace VixenModules.SmartController.VideoScreen
{
	public class VideoScreenControllerDescriptor : SmartControllerModuleDescriptorBase
	{
		private static Guid _typeId = new Guid("B918AC1F-031F-4AF7-9088-0394C1FA7BEE");

		public override string TypeName => "Video Screen Controller";

		public override Guid TypeId => _typeId;

		public override Type ModuleClass => typeof(VideoScreenControllerModule);

		public override string Author => "Jeff Uchitjil";

		public override string Description => "A video screen controller that will display video data to and output window.";

		public override string Version => "1.0";
	}
}