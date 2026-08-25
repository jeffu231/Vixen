namespace Vixen.Sys.Engine
{
	/// <summary>
	/// Releases one scheduler quiescence request when disposed.
	/// </summary>
	internal sealed class ExecutionQuiesceLease(Action release) : IDisposable
	{
		private Action _release = release ?? throw new ArgumentNullException(nameof(release));

		/// <inheritdoc />
		public void Dispose()
		{
			Interlocked.Exchange(ref _release, null)?.Invoke();
		}
	}
}
