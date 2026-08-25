namespace Vixen.Sys.Output
{
	/// <summary>
	/// Core abstraction for the in-memory output device.
	/// </summary>
	public interface IOutputDevice : IHardware, IHasSetup
	{
		Guid Id { get; }
		Guid ModuleId { get; }
		string Name { get; set; }
		void Update();
		/// <summary>
		/// Only update the commands, do not send them out.
		/// </summary>
		void UpdateCommands();
		/// <summary>Gets or sets the legacy per-device update interval.</summary>
		/// <remarks>Migrate scheduler configuration to <see cref="VixenSystem.DefaultUpdateInterval" />. VIX-3750 ignores this value.</remarks>
		[Obsolete("Per-device scheduling is no longer supported. The execution scheduler uses VixenSystem.DefaultUpdateInterval.")]
		int UpdateInterval { get; set; }

		/// <summary>Gets the legacy per-device update signaler.</summary>
		/// <remarks>Do not signal output updates directly. The execution scheduler controls frame dispatch.</remarks>
		[Obsolete("Per-device update signaling is no longer supported. The execution scheduler controls frame dispatch.")]
		IOutputDeviceUpdateSignaler UpdateSignaler { get; }
	}
}
