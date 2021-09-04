using System;
using Vixen.Module.OutputFilter;

namespace VixenModules.OutputFilter.PositionBreakdown
{
	public class PositionBreakdownDescriptor : OutputFilterModuleDescriptorBase
	{
		private static readonly Guid _typeId = new Guid("{CA65CF74-5A6E-4AA6-B122-6707C064D71D}");

		public override string TypeName => "Position Breakdown";

		public override Guid TypeId => _typeId;

		public static Guid ModuleId => _typeId;

		public override Type ModuleClass => typeof (PositionBreakdownModule);

		public override Type ModuleDataClass => typeof (PositionBreakdownData);

		public override string Author => "Vixen Team";

		public override string Description => "An output filter that breaks down position intents into the Pan and Tilt components.";

		public override string Version => "1.0";
	}
}