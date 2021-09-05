using System;
using System.Drawing;

namespace Vixen.Data.Value
{
    public struct BitmapValue : IIntentDataType
	{
		public BitmapValue(Bitmap image)
		{
			Image = image;
		}

		/// <summary>
		/// A System.Drawing.Bitmap object
		/// </summary>
		public Bitmap Image;
    }
}