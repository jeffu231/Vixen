namespace Vixen.Sys.Engine
{
	internal interface IPreviewFrameRenderer
	{
		void Post(Action callback);
		void Render(PreviewFrameSnapshot snapshot);
	}
}
