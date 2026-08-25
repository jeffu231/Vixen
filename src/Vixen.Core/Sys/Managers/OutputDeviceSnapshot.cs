using Vixen.Sys.Output;

namespace Vixen.Sys.Managers
{
	internal sealed class OutputDeviceSnapshot<T> : IDisposable
		where T : class, IOutputDevice
	{
		private readonly Action<T[]> _release;
		private bool _isDisposed;

		internal OutputDeviceSnapshot(T[] devices, Action<T[]> release)
		{
			ArgumentNullException.ThrowIfNull(devices);
			ArgumentNullException.ThrowIfNull(release);
			Devices = devices;
			_release = release;
		}

		public T[] Devices { get; }

		public void Dispose()
		{
			if (_isDisposed)
			{
				return;
			}

			_release(Devices);
			_isDisposed = true;
		}
	}
}
