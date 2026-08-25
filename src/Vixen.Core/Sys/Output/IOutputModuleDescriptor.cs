using Vixen.Module;

namespace Vixen.Sys.Output
{
	public interface IOutputModuleDescriptor : IModuleDescriptor
	{
		/// <summary>Gets the legacy module update interval.</summary>
		/// <remarks>Migrate scheduler configuration to <see cref="VixenSystem.DefaultUpdateInterval" />.</remarks>
		[Obsolete("Per-device scheduling is no longer supported. The execution scheduler uses VixenSystem.DefaultUpdateInterval.")]
		int UpdateInterval { get; }
	}
}
