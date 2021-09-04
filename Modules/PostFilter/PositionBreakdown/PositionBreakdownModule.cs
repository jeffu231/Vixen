using System;
using System.Windows.Forms;
using Vixen.Data.Flow;
using Vixen.Data.Value;
using Vixen.Module;
using Vixen.Module.OutputFilter;

namespace VixenModules.OutputFilter.PositionBreakdown
{
	public class PositionBreakdownModule : OutputFilterModuleInstanceBase
	{
		private PositionBreakdownData _data;
		private PositionBreakdownOutput[] _outputs;

		public override void Handle(IntentsDataFlowData obj)
		{
			foreach (PositionBreakdownOutput output in _outputs) {
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
				_data = (PositionBreakdownData) value;
				_CreateOutputs();
			}
		}

		public override bool HasSetup => true;

		public override bool Setup()
		{
			using (PositionBreakdownSetup setup = new PositionBreakdownSetup(_data)) {
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
				_outputs = new[] { new PositionBreakdownOutput(PositionType.Pan), new PositionBreakdownOutput(PositionType.Tilt) };
			}else if (_data.CanPan)
			{
				_outputs = new[] { new PositionBreakdownOutput(PositionType.Pan) };
			}else if(_data.CanTilt)
			{
				_outputs = new[] { new PositionBreakdownOutput(PositionType.Tilt) };
			}
			else
			{
				_outputs = Array.Empty<PositionBreakdownOutput>();
			}
			
		}
	}

}