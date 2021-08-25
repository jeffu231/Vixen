using System;
using Vixen.Module.Property;

namespace VixenModules.Property.Tilt
{
	public class TiltDescriptor : PropertyModuleDescriptorBase
	{
		public static Guid _typeId = new Guid("{799A9DEE-B5BB-4A20-BA72-8E93B72BD472}");

		public override string TypeName => "Tilt";

		public override Guid TypeId => _typeId;

		public override Type ModuleClass => typeof (TiltModule);

		public override string Author => "Vixen Team";

		public override string Description => "Provides ability to specify an element can Tilt";

		public override string Version => "1.0";

		public override Type ModuleStaticDataClass => typeof (TiltData);
	}
}