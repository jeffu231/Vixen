using System;
using System.Collections.Generic;
using System.Linq;
using Vixen.Extensions;
using Vixen.Module.SmartController;
using Vixen.Sys;
using VixenModules.SmartController.VideoScreen.WPF;

namespace VixenModules.SmartController.VideoScreen
{
	public class VideoScreenControllerModule : SmartControllerModuleInstanceBase
	{
		private List<VideoWindowWpfContainer> _outputWindows;
		public override void Start()
		{
			base.Start();

			_outputWindows = new List<VideoWindowWpfContainer>(OutputCount);

			for (int i = 0; i < OutputCount; i++)
			{
				var w = new VideoWindowWpfContainer();
				_outputWindows.Add(w);
				w.DisplayName = $"Output {i}";
				w.Setup(640,480);
				w.Show();
			}
		}

		public override void Stop()
		{
			base.Stop();

			foreach (var videoWindowWpfContainer in _outputWindows)
			{
				if (videoWindowWpfContainer != null && !videoWindowWpfContainer.IsDisposed)
				{
					videoWindowWpfContainer.Hide();
					videoWindowWpfContainer.Dispose();
				}
			}

			_outputWindows.Clear();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				foreach (var videoWindowWpfContainer in _outputWindows)
				{
					if (videoWindowWpfContainer != null && !videoWindowWpfContainer.IsDisposed)
					{
						videoWindowWpfContainer.Dispose();
					}
				}
			}

			base.Dispose(disposing);
		}

		#region Overrides of SmartControllerModuleInstanceBase

		private List<IIntentState> _tempState = new List<IIntentState>(4);
		/// <inheritdoc />
		public override void UpdateState(IntentChangeCollection[] outputStates)
		{
			if (outputStates != null)
			{
				//foreach (var intentChangeCollection in outputStates)
				//{
				//	if (intentChangeCollection != null)
				//	{
				//		Console.Out.WriteLine($"Added: {intentChangeCollection.AddedIntents.Length} Removed: {intentChangeCollection.RemovedIntents.Length}");
				//	}
				//}
				for (int i = 0; i < outputStates.Length; i++)
				{
					if (outputStates[i] != null)
					{
						_tempState = _outputWindows[i].State.ToList();
						_tempState.RemoveAll(outputStates[i].RemovedIntents);
						_tempState.AddRange(outputStates[i].AddedIntents);
						_outputWindows[i].State = _tempState.ToArray();
						_outputWindows[i].UpdateWindow();
						_tempState.Clear();
					}
					else
					{
						if (_outputWindows[i].State.Length > 0)
						{
							_outputWindows[i].UpdateWindow();
						}
					}
				}
			}
			
			
		}

		/// <inheritdoc />
		public override int OutputCount { get; set; }

		#endregion
	}
}