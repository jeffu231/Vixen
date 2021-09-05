using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Drawing.Color;
using Rectangle = System.Drawing.Rectangle;

namespace VixenModules.Preview.VideoPreview.WPF.View
{
	/// <summary>
	/// Interaction logic for ImageViewer.xaml
	/// </summary>
	public partial class ImageViewer : UserControl
	{
		private int _width, _height;
		private WriteableBitmap _writeableBitmap;

		public ImageViewer()
		{
			InitializeComponent();
			
			RenderOptions.SetBitmapScalingMode(Image, BitmapScalingMode.NearestNeighbor);
			RenderOptions.SetEdgeMode(Image, EdgeMode.Aliased);
		}

		public void Configure(int width, int height)
		{
			_writeableBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
			Image.Source = _writeableBitmap;
		}

		public void Update(Bitmap bmp)
		{
			if (!CheckAccess())
			{
				Dispatcher.Invoke(() => TransferImage(bmp));
			}
			else
			{
				TransferImage(bmp);
			}
		}

		public void Clear()
		{
			if (!CheckAccess())
			{
				Dispatcher.Invoke(() => ClearImage());
			}
			else
			{
				ClearImage();
			}
		}

		private void ClearImage()
		{
			using (_writeableBitmap.GetBitmapContext())
			{
				_writeableBitmap.Clear();
			}
		}

		private void TransferImage(Bitmap bmp)
		{
			lock (bmp)
			{
				BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, bmp.PixelFormat);
				try
				{
					var rect = new Int32Rect(0, 0, bmp.Width, bmp.Height);
					_writeableBitmap.WritePixels(rect, data.Scan0, bmp.Height * data.Stride, data.Stride);
				}
				finally
				{
					bmp.UnlockBits(data);
				}
			}
		}
	}
}
