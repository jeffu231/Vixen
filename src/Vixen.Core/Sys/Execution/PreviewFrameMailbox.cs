using NLog;
using System.Diagnostics;

namespace Vixen.Sys.Engine
{
	internal sealed class PreviewFrameMailbox : IPreviewFramePublisher
	{
		private static readonly Logger Logging = LogManager.GetCurrentClassLogger();
		private readonly object _syncRoot = new();
		private readonly Action<Action> _post;
		private readonly Action<PreviewFrameSnapshot> _render;
		private PreviewFrameSnapshot _latestSnapshot;
		private bool _hasPendingSnapshot;
		private bool _isCallbackPending;
		private long _invalidationVersion;

		public PreviewFrameMailbox(Action<Action> post, Action<PreviewFrameSnapshot> render)
		{
			ArgumentNullException.ThrowIfNull(post);
			ArgumentNullException.ThrowIfNull(render);
			_post = post;
			_render = render;
		}

		public long CoalescedFrameCount { get; private set; }
		public long LastRenderedFrameId { get; private set; }
		public long LastFrameAgeTicks { get; private set; }
		public long LastRenderDurationTicks { get; private set; }

		public bool IsCallbackPending
		{
			get
			{
				lock (_syncRoot) return _isCallbackPending;
			}
		}

		public long LatestFrameId
		{
			get
			{
				lock (_syncRoot) return _hasPendingSnapshot ? _latestSnapshot.FrameId : 0;
			}
		}

		public void Publish(PreviewFrameSnapshot snapshot)
		{
			var shouldPost = false;
			lock (_syncRoot)
			{
				if (_hasPendingSnapshot) CoalescedFrameCount++;
				_latestSnapshot = snapshot;
				_hasPendingSnapshot = true;
				if (!_isCallbackPending)
				{
					_isCallbackPending = true;
					shouldPost = true;
				}
			}

			if (shouldPost) PostCallback();
		}

		public void Invalidate()
		{
			lock (_syncRoot)
			{
				_invalidationVersion++;
				_hasPendingSnapshot = false;
			}
		}

		private void PostCallback()
		{
			try
			{
				_post(ProcessLatestSnapshot);
			}
			catch (Exception exception)
			{
				Logging.Error(exception, "Unable to post preview frame callback.");
				lock (_syncRoot) _isCallbackPending = false;
			}
		}

		private void ProcessLatestSnapshot()
		{
			PreviewFrameSnapshot snapshot;
			long invalidationVersion;
			lock (_syncRoot)
			{
				if (!_hasPendingSnapshot)
				{
					_isCallbackPending = false;
					return;
				}

				snapshot = _latestSnapshot;
				invalidationVersion = _invalidationVersion;
				_hasPendingSnapshot = false;
			}

			try
			{
				if (invalidationVersion == Volatile.Read(ref _invalidationVersion))
				{
					var renderStartTimestamp = Stopwatch.GetTimestamp();
					_render(snapshot);
					LastRenderedFrameId = snapshot.FrameId;
					LastFrameAgeTicks = Math.Max(0, renderStartTimestamp - snapshot.PublicationTimestamp);
					LastRenderDurationTicks = Math.Max(0, Stopwatch.GetTimestamp() - renderStartTimestamp);
				}
			}
			catch (Exception exception)
			{
				Logging.Error(exception, "Preview failed to render frame {0}.", snapshot.FrameId);
			}

			var shouldPost = false;
			lock (_syncRoot)
			{
				if (_hasPendingSnapshot)
				{
					shouldPost = true;
				}
				else
				{
					_isCallbackPending = false;
				}
			}

			if (shouldPost) PostCallback();
		}
	}
}
