namespace Vixen.Sys.Output
{
	/// <summary>Represents the obsolete device-owned output update signaler.</summary>
	/// <remarks>VIX-3750 dispatches output frames from the central execution scheduler. Do not implement new signalers.</remarks>
	[Obsolete("Per-device update signaling is no longer supported. The execution scheduler controls frame dispatch.")]
	public interface IOutputDeviceUpdateSignaler
	{
		IOutputDevice OutputDevice { set; }
		EventWaitHandle UpdateSignal { set; }
		void RaiseSignal();
	}
}
