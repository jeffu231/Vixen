using System;
using Vixen.Module.Property;

namespace VixenModules.Property.Pan
{
	public class PanDescriptor : PropertyModuleDescriptorBase
	{
		public static Guid _typeId = new Guid("{C3E11F87-03FB-4E47-804D-39CF46256AC1}");

		public override string TypeName => "Pan";

		public override Guid TypeId => _typeId;

		public override Type ModuleClass => typeof (PanModule);

		public override string Author => "Vixen Team";

		public override string Description => "Provides ability to specify an element can Pan";

		public override string Version => "1.0";

		public override Type ModuleStaticDataClass => typeof (PanData);
	}
}