using Vixen.Execution;
using Vixen.Execution.Context;
using Vixen.Sys;
using Vixen.Sys.Output;
using Vixen.Sys.Engine;

namespace Vixen.Cache.Sequence
{
	public class SequenceIntervalGenerator
	{
		private static readonly NLog.Logger Logging = NLog.LogManager.GetCurrentClassLogger();
		private bool _statesClear;
		private PreCachingSequenceContext _context;
		private IReadOnlyList<ContextExecutionState> _contextStates = [];
		private IReadOnlyList<OutputDeviceExecutionState> _outputDeviceStates = [];
		private ExecutionQuiesceLease _quiesceLease;
		private bool _generationStarted;
		private int _outputCount;
		
		#region Contructors

		/// <summary>
		/// Create using the default update interval
		/// </summary>
		public SequenceIntervalGenerator()
		{	
			State = new OutputStateList();
			TimingSource = new FixedIntervalManualTiming();
		}

		/// <summary>
		/// Create using a specified sequence and default interval.
		/// </summary>
		/// <param name="sequence"></param>
		public SequenceIntervalGenerator(ISequence sequence):this()
		{
			Sequence = sequence;
		}

		/// <summary>
		/// Create using a specified update interval.
		/// </summary>
		/// <param name="interval"></param>
		public SequenceIntervalGenerator(int interval)
			: this()
		{
			TimingSource.Interval = interval;
		}

		/// <summary>
		/// Create using a specified update interval and sequence.
		/// </summary>
		/// <param name="interval"></param>
		/// <param name="sequence"></param>
		public SequenceIntervalGenerator(int interval, ISequence sequence)
			: this(interval)
		{
			Sequence = sequence;
		}

		#endregion

		#region Properties


		private FixedIntervalManualTiming TimingSource { get; set; }

		public ISequence Sequence { get; set; }

		public int Interval
		{
			get { return TimingSource.Interval; }
			set { TimingSource.Interval = value; }
		}

		public TimeSpan Position
		{
			get { return TimingSource.Position; }
		}

		#endregion

		#region Operational

		public bool HasNextInterval()
		{
			return TimingSource.Position + TimeSpan.FromMilliseconds(TimingSource.Interval) <= Sequence.Length;
		}

		public OutputStateList State { get; set; }

		/// <summary>
		/// Gets the command outputs for the current interval and then increments the timing source.
		/// </summary>
		/// <returns>Command outputs or null if there are no more intervals.</returns>
		public void NextInterval()
		{
			//Advance the timing
			TimingSource.Increment();
			UpdateState();
		}

		private void UpdateState()
		{
			if (HasNextInterval())
			{
				List<CommandOutput> commands = _UpdateState(TimingSource.Position);
				State.AddCommands(commands);
			}
		}

		/// <summary>
		/// Acquires exclusive execution state and initializes data extraction.
		/// </summary>
		/// <exception cref="InvalidOperationException">Generation is already active for this instance.</exception>
		/// </summary>
		public void BeginGeneration()
		{
			if (_generationStarted) throw new InvalidOperationException("Generation has already started.");

			_quiesceLease = Sys.Execution.Quiesce();
			try
			{
				_contextStates = VixenSystem.Contexts
					.Select(context => new ContextExecutionState(context, context.IsRunning, context.IsPaused))
					.ToArray();
				_outputDeviceStates = VixenSystem.OutputDeviceManagement.Devices
					.Select(device => new OutputDeviceExecutionState(device, device.IsRunning, device.IsPaused))
					.ToArray();

				PauseRunningOutputDevices();
				PauseRunningContexts();
				_outputCount = VixenSystem.OutputControllers.GetAll().Sum(x => x.OutputCount);
				VixenSystem.Elements.ClearStates();
				_context = VixenSystem.Contexts.GetCacheCompileContext();
				_context.Sequence = Sequence;
				_context.Start();
				TimingSource.Start();
				_generationStarted = true;
				UpdateState();
			}
			catch
			{
				EndGeneration();
				throw;
			}
		}

		/// <summary>
		/// Completes generation and restores the output devices and contexts captured by <see cref="BeginGeneration" />.
		/// </summary>
		/// </summary>
		public void EndGeneration()
		{
			try
			{
				TimingSource.Stop();
				VixenSystem.Elements.ClearStates();
				VixenSystem.Filters.Update();
			}
			finally
			{
				ReleaseCompilerContext();
				RestoreContextStates();
				RestoreOutputDeviceStates();
				_generationStarted = false;
				_contextStates = [];
				_outputDeviceStates = [];
				_quiesceLease?.Dispose();
				_quiesceLease = null;
			}
		}

		private void ReleaseCompilerContext()
		{
			if (_context == null) return;
			try
			{
				VixenSystem.Contexts.ReleaseContext(_context);
			}
			catch (Exception exception)
			{
				Logging.Error(exception, "Failed to release the compiler context after export.");
			}
			finally
			{
				_context = null;
			}
		}

		private void PauseRunningOutputDevices()
		{
			foreach (var state in _outputDeviceStates.Where(state => state.IsRunning && !state.IsPaused))
			{
				PauseOutputDevice(state.Device);
			}
		}

		private void PauseRunningContexts()
		{
			foreach (var state in _contextStates.Where(state => state.IsRunning && !state.IsPaused))
			{
				state.Context.Pause();
			}
		}

		private void RestoreContextStates()
		{
			foreach (var state in _contextStates)
			{
				try
				{
					if (state.IsRunning && !state.IsPaused && state.Context.IsRunning && state.Context.IsPaused)
					{
						state.Context.Resume();
					}
				}
				catch (Exception exception)
				{
					Logging.Error(exception, "Failed to restore context {0} after export.", state.Context.Name);
				}
			}
		}

		private void RestoreOutputDeviceStates()
		{
			foreach (var state in _outputDeviceStates)
			{
				try
				{
					if (state.IsRunning && !state.IsPaused && state.Device.IsRunning && state.Device.IsPaused)
					{
						ResumeOutputDevice(state.Device);
					}
				}
				catch (Exception exception)
				{
					Logging.Error(exception, "Failed to restore output device {0} after export.", state.Device.Name);
				}
			}
		}

		private static void PauseOutputDevice(IOutputDevice outputDevice)
		{
			switch (outputDevice)
			{
				case OutputController controller:
					VixenSystem.OutputControllers.Pause(controller);
					break;
				case OutputPreview preview:
					VixenSystem.Previews.Pause(preview);
					break;
			}
		}

		private static void ResumeOutputDevice(IOutputDevice outputDevice)
		{
			switch (outputDevice)
			{
				case OutputController controller:
					VixenSystem.OutputControllers.Resume(controller);
					break;
				case OutputPreview preview:
					VixenSystem.Previews.Resume(preview);
					break;
			}
		}


		private List<CommandOutput> _UpdateState(TimeSpan time)
		{
			var outputCommands = new List<CommandOutput>(_outputCount);

			//Advance our context to specified time and do all the normal update stuff
			bool elementsAffected = VixenSystem.Contexts.UpdateCacheCompileContext(time, _context);
			//Check to see if any elements are affected
			if (elementsAffected)
			{
				VixenSystem.Elements.Update();
				_statesClear = false;
				VixenSystem.Filters.Update();
			}
			else if (!_statesClear)
			{
				//Nothing is happening so clear out the states instead of sampling empty context interval
				VixenSystem.Elements.ClearStates();
				_statesClear = true;
				VixenSystem.Filters.Update();
			}

			//Now walk the outputs and collect our data
			foreach (OutputController outputController in VixenSystem.OutputControllers.GetAll())
			{
				outputController.UpdateCommands();
				outputCommands.AddRange(outputController.Outputs);
			}

			return outputCommands;
		}
		
		#endregion	
	}
}
