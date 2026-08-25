using Vixen.Sys.Output;

namespace Vixen.Module.Preview
{
	public abstract class PreviewModuleInstanceBase : OutputModuleInstanceBase, IPreviewModuleInstance,
	                                                  IEqualityComparer<IPreviewModuleInstance>,
	                                                  IEquatable<IPreviewModuleInstance>,
	                                                  IEqualityComparer<PreviewModuleInstanceBase>,
	                                                  IEquatable<PreviewModuleInstanceBase>,
	                                                  Vixen.Sys.Engine.IPreviewFrameRenderer
	{
		protected abstract IThreadBehavior ThreadBehavior { get; }

		//protected Vixen.Preview.PreviewElementIntentStates ElementStates { get; private set; }

		public override void Start()
		{
			ThreadBehavior.Start();
			base.Start();
			base.Resume();
		}

		public override void Stop()
		{
			ThreadBehavior.Stop();
			base.Stop();
			base.Resume();
		}

		public override bool IsRunning
		{
			get { return ThreadBehavior.IsRunning; }
		}

		/// <summary>
		/// Queues a best-effort render of the current live element state.
		/// </summary>
		/// <remarks>
		/// The execution engine uses its coalescing preview mailbox for frame delivery. This legacy entry point remains for callers
		/// that request a direct preview update.
		/// </remarks>
		public void UpdateState(/*Vixen.Preview.PreviewElementIntentStates elementIntentStates*/)
		{
			// Get the data referenced locally so we can get off this thread if need be.
			//ElementStates = elementIntentStates;
			if(IsRunning)
			{
				ThreadBehavior.BeginInvoke(Update);
			}
		}

		void Vixen.Sys.Engine.IPreviewFrameRenderer.Post(Action callback)
		{
			if (IsRunning && !IsPaused)
			{
				ThreadBehavior.BeginInvoke(callback);
			}
		}

		void Vixen.Sys.Engine.IPreviewFrameRenderer.Render(Vixen.Sys.Engine.PreviewFrameSnapshot snapshot)
		{
			if (IsRunning && !IsPaused)
			{
				Update();
			}
		}

		/// <inheritdoc />
		public void PlayerStarted()
		{
			if (IsRunning)
			{
				ThreadBehavior.BeginInvoke(PlayerActivatedImpl);
			}
		}

		/// <inheritdoc />
		public void PlayerEnded()
		{
			if (IsRunning)
			{
				ThreadBehavior.BeginInvoke(PlayerDeactivatedImpl);
			}
		}

		protected abstract void PlayerActivatedImpl();

		protected abstract void PlayerDeactivatedImpl();

		protected abstract void Update();

		#region Equality

		public bool Equals(IPreviewModuleInstance x, IPreviewModuleInstance y)
		{
			return base.Equals(x, y);
		}

		public int GetHashCode(IPreviewModuleInstance obj)
		{
			return base.GetHashCode(obj);
		}

		public bool Equals(IPreviewModuleInstance other)
		{
			return base.Equals(other);
		}

		public bool Equals(PreviewModuleInstanceBase x, PreviewModuleInstanceBase y)
		{
			return Equals(x, y as IPreviewModuleInstance);
		}

		public int GetHashCode(PreviewModuleInstanceBase obj)
		{
			return GetHashCode(obj as IPreviewModuleInstance);
		}

		public bool Equals(PreviewModuleInstanceBase other)
		{
			return Equals(other as IPreviewModuleInstance);
		}

		public virtual string Name { get; set; }

		#endregion
	}
}
