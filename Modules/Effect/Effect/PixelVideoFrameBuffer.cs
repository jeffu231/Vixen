using System.Drawing;
using Common.Controls.ColorManagement.ColorModels;

namespace VixenModules.Effect.Effect
{
	public class PixelVideoFrameBuffer:IPixelFrameBuffer
	{
		private FastPixel.FastPixel _fp;
		private readonly int _width;
		private readonly int _height;
		private readonly Color _baseColor;

		public PixelVideoFrameBuffer(int width, int height, Color baseColor)
		{
			_width = width;
			_height = height;
			_baseColor = baseColor;
			Reset();
		}

		public PixelVideoFrameBuffer(int width, int height):this(width, height, Color.Empty)
		{
			
		}

		public void Reset()
		{
			if (_fp != null && _fp.locked)
			{
				_fp.Unlock(false);
			}
			_fp = new FastPixel.FastPixel(_width, _height);
		}

		public void ClearBuffer()
		{
			if (_baseColor != Color.Empty)
			{
				_fp.Clear(_baseColor);
			}
		}

		public void ClearBuffer(double level)
		{
			var hsv = HSV.FromRGB(_baseColor);
			hsv.V = hsv.V * level;
			for (int i = 0; i < _width; i++)
			{
				for (int z = 0; z < _height; z++)
				{
					_fp.SetPixelWithAlpha(i,z,hsv.ToRGB());
				}
			}
		}

		public void BeginUpdates()
		{
			_fp.Lock();
		}

		public void EndUpdates()
		{
			_fp.Unlock(true);
		}

		public Bitmap Bitmap => _fp.Bitmap;

		#region Implementation of IPixelFrameBuffer

		/// <inheritdoc />
		public void SetPixel(int x, int y, Color c)
		{
			_fp.SetPixelWithAlpha(x, _height - 1 - y, c);
		}

		/// <inheritdoc />
		public void SetPixel(int x, int y, HSV hsv)
		{
			Color color = hsv.ToRGB();
			SetPixel(x, y, color);
		}

		/// <inheritdoc />
		public Color GetColorAt(int x, int y)
		{
			return _fp.GetPixel(x, _height - 1 - y);
		}

		#endregion
	}
}
