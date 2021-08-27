using System;
using Vixen.Module.Effect;
using Vixen.Sys;

namespace VixenModules.Effect.SetPosition
{
	public class SetPositionDescriptor : EffectModuleDescriptorBase
	{
		private Guid _typeId = new Guid("{9B6D85EC-F16B-41f2-8584-8E85211E02B8}");

		public override string TypeName => "Set position";

		public override Guid TypeId => _typeId;

		public override Type ModuleClass => typeof (SetPositionModule);

		public override Type ModuleDataClass => typeof (SetPositionData);

		public override string Author => "Vixen Team";

		public override string Description => "Set the position of a positionable device";

		public override string Version => "1.0";

		public override string EffectName => "Set Position";

		/// <inheritdoc />
		public override ParameterSignature Parameters { get; }

		/// <inheritdoc />
		public override EffectGroups EffectGroup => EffectGroups.Device;
	}
}