using System;
using System.Windows.Forms;
using Vixen.Data.Flow;
using Vixen.Module;
using Vixen.Module.OutputFilter;

namespace VixenModules.OutputFilter.PanTiltBreakdown
{
	public class PanTiltBreakdownModule : OutputFilterModuleInstanceBase
	{
		private PanTiltBreakdownData _data;
		private PanTiltBreakdownOutput[] _outputs;

		public override void Handle(IntentsDataFlowData obj)
		{
			foreach (PanTiltBreakdownOutput output in _outputs) {
				output.ProcessInputData(obj);
			}
		}

		public override DataFlowType InputDataType => DataFlowType.MultipleIntents;

		public override DataFlowType OutputDataType => DataFlowType.MultipleIntents;

		public override IDataFlowOutput[] Outputs => _outputs;

		public override IModuleDataModel ModuleData
		{
			get => _data;
			set
			{
				_data = (PanTiltBreakdownData) value;
				_CreateOutputs();
			}
		}

		public override bool HasSetup => true;

		public override bool Setup()
		{
			using (PanTiltBreakdownSetup setup = new PanTiltBreakdownSetup(_data)) {
				if (setup.ShowDialog() == DialogResult.OK) {
					_data.CanPan = setup.CanPan;
					_data.CanTilt = setup.CanTilt;
					_CreateOutputs();
					return true;
				}
			}
			return false;
		}

		private void _CreateOutputs()
		{
			if (_data.CanPan && _data.CanTilt)
			{
				_outputs = new[] { new PanTiltBreakdownOutput(PositionType.Pan), new PanTiltBreakdownOutput(PositionType.Tilt) };
			}else if (_data.CanPan)
			{
				_outputs = new[] { new PanTiltBreakdownOutput(PositionType.Pan) };
			}else if(_data.CanTilt)
			{
				_outputs = new[] { new PanTiltBreakdownOutput(PositionType.Tilt) };
			}
			else
			{
				_outputs = Array.Empty<PanTiltBreakdownOutput>();
			}
			
		}
	}

}