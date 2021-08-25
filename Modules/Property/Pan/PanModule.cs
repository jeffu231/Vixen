using System.Windows.Forms;
using Vixen.Module;
using Vixen.Module.Property;

namespace VixenModules.Property.Pan
{
	public class PanModule : PropertyModuleInstanceBase
	{
		private PanData _data;

		public override void SetDefaultValues()
		{
			_data.Range = 360;
		}

		public override bool HasSetup => true;

		public override bool Setup()
		{
			using (PanSetup setupForm = new PanSetup(_data.Range)) {
				if (setupForm.ShowDialog() == DialogResult.OK)
				{

					_data.Range = setupForm.Range;
					return true;
				}
				return false;
			}
		}

		public override IModuleDataModel StaticModuleData
		{
			get => _data;
			set => _data = value as PanData;
		}

	}
}