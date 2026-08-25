namespace Vixen.Sys.Engine
{
	internal interface IPreviewFramePublisher
	{
		void Publish(PreviewFrameSnapshot snapshot);
		void Invalidate();
	}
}
