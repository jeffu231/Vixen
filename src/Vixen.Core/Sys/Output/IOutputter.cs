namespace Vixen.Sys.Output
{
	/// <summary>
	/// Core abstraction for an output-device module.
	/// </summary>
	public interface IOutputter : IHasSetup, IHardware
	{
		/// <summary>Gets the legacy module-owned update interval.</summary>
		/// <remarks>Migrate scheduler configuration to <see cref="VixenSystem.DefaultUpdateInterval" />.</remarks>
		[Obsolete("Per-device scheduling is no longer supported. The execution scheduler uses VixenSystem.DefaultUpdateInterval.")]
		int UpdateInterval { get; }

		/// <summary>Gets the legacy module-owned update signaler.</summary>
		/// <remarks>Do not signal output updates directly. The execution scheduler controls frame dispatch.</remarks>
		[Obsolete("Per-device update signaling is no longer supported. The execution scheduler controls frame dispatch.")]
		IOutputDeviceUpdateSignaler UpdateSignaler { get; }

		/// <summary>
		/// Controller modules should specify true if they want to set their own output names
		/// </summary>
		bool SupportsNamedOutputs { get; }

		/// <summary>
		/// Controller modules can override this to add their own naming to the outputs
		/// </summary>
		void NameOutputs();
	}
}
