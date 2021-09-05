using System.Drawing;
using Vixen.Data.Value;
using Vixen.Sys;
using Vixen.Sys.Dispatch;

namespace VixenModules.Preview.VideoPreview.Dispatch
{
	public class BitmapIntentHandler: IntentStateDispatch
	{
		private Bitmap _bitmap = null;

		public Bitmap GetImage(IIntentState state)
		{
			_bitmap = null;
			state?.Dispatch(this);
			return _bitmap; ;
		}

		#region Overrides of IntentStateDispatch

		/// <inheritdoc />
		public override void Handle(IIntentState<BitmapValue> obj)
		{
			_bitmap = obj.GetValue().Image;
		}

		#endregion
	}
}
