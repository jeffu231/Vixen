using Vixen.Sys.Engine;
using Xunit;

namespace Vixen.Tests.Sys.Engine;

public class PreviewFrameMailboxTests
{
	[Fact]
	public void Publish_CoalescesPendingFramesAndRendersLatestFrame()
	{
		// Arrange
		var callbacks = new Queue<Action>();
		var renderedFrameIds = new List<long>();
		var mailbox = new PreviewFrameMailbox(callbacks.Enqueue, snapshot => renderedFrameIds.Add(snapshot.FrameId));

		// Act
		mailbox.Publish(new PreviewFrameSnapshot(1, 100));
		mailbox.Publish(new PreviewFrameSnapshot(2, 200));
		callbacks.Dequeue()();

		// Assert
		Assert.Empty(callbacks);
		Assert.Equal([2], renderedFrameIds);
		Assert.Equal(1, mailbox.CoalescedFrameCount);
		Assert.Equal(2, mailbox.LastRenderedFrameId);
		Assert.True(mailbox.LastFrameAgeTicks >= 0);
		Assert.True(mailbox.LastRenderDurationTicks >= 0);
		Assert.False(mailbox.IsCallbackPending);
	}

	[Fact]
	public void Publish_DuringRenderSchedulesOneFollowUpForLatestFrame()
	{
		// Arrange
		var callbacks = new Queue<Action>();
		var renderedFrameIds = new List<long>();
		PreviewFrameMailbox mailbox = null!;
		mailbox = new PreviewFrameMailbox(callbacks.Enqueue, snapshot =>
		{
			renderedFrameIds.Add(snapshot.FrameId);
			if (snapshot.FrameId == 1)
			{
				mailbox.Publish(new PreviewFrameSnapshot(2, 200));
				mailbox.Publish(new PreviewFrameSnapshot(3, 300));
			}
		});

		// Act
		mailbox.Publish(new PreviewFrameSnapshot(1, 100));
		callbacks.Dequeue()();
		callbacks.Dequeue()();

		// Assert
		Assert.Empty(callbacks);
		Assert.Equal([1, 3], renderedFrameIds);
		Assert.Equal(1, mailbox.CoalescedFrameCount);
		Assert.False(mailbox.IsCallbackPending);
	}

	[Fact]
	public void Invalidate_PreventsQueuedFrameFromRendering()
	{
		// Arrange
		var callbacks = new Queue<Action>();
		var renderedFrameIds = new List<long>();
		var mailbox = new PreviewFrameMailbox(callbacks.Enqueue, snapshot => renderedFrameIds.Add(snapshot.FrameId));

		// Act
		mailbox.Publish(new PreviewFrameSnapshot(1, 100));
		mailbox.Invalidate();
		callbacks.Dequeue()();

		// Assert
		Assert.Empty(renderedFrameIds);
		Assert.False(mailbox.IsCallbackPending);
	}

	[Fact]
	public void RenderFailure_DoesNotPreventLaterPublication()
	{
		// Arrange
		var callbacks = new Queue<Action>();
		var renderCount = 0;
		var mailbox = new PreviewFrameMailbox(callbacks.Enqueue, _ =>
		{
			renderCount++;
			throw new InvalidOperationException("Preview render failure");
		});

		// Act
		mailbox.Publish(new PreviewFrameSnapshot(1, 100));
		callbacks.Dequeue()();
		mailbox.Publish(new PreviewFrameSnapshot(2, 200));
		callbacks.Dequeue()();

		// Assert
		Assert.Equal(2, renderCount);
		Assert.False(mailbox.IsCallbackPending);
	}
}
