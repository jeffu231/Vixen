namespace Vixen.Sys.State.Execution.Behavior
{
	internal class StandardClosingBehavior
	{
		public static void Run()
		{
			Sys.Execution.Shutdown();

			// Release all contexts.
			VixenSystem.Contexts.ReleaseContexts();

			// Stop all output devices.
			VixenSystem.OutputDeviceManagement.StopAll();
		}
	}
}
