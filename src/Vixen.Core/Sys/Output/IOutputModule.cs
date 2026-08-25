using Vixen.Module;

namespace Vixen.Sys.Output
{
	/// <summary>
	/// Defines the module used by an <see cref="IOutputDevice" /> and the basis for output modules.
	/// </summary>
	/// <remarks>
	/// The inherited per-device update interval and signaler are retained only for compatibility. New modules must rely on the central execution scheduler.
	/// </remarks>
	public interface IOutputModule : IModuleInstance, IOutputter
	{
	}
}
