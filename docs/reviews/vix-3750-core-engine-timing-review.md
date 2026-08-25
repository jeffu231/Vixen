# VIX-3750 Core Engine Timing Review

## Scope

Reviewed commits `bf41bf94add99cdb4607ac6b1a88d9fd09d2b88e` through
`96980f1948fb0adbdafa619ec916025f615680de`, inclusive.

VIX-3750 describes controller-owned sleep/petition loops producing irregular engine updates, especially when only the
preview is running. The proposed direction is one conditional engine update loop, a consistent state snapshot for all
controllers, genuinely parallel asynchronous controller output, and a controller-completion barrier before the next
engine frame. Preview rendering is best-effort: it should reflect the latest published engine state without queuing one
render operation per engine frame or delaying controller output.

The branch implements the central loop and removes the controller-owned update threads. That is the right overall
direction, but the current lifecycle, pause, export, and timing behavior has several release-blocking risks.

## Findings

### High: Paused controllers and previews continue to receive frame updates

`Execution.UpdateOutputDevicesAsync()` and `Execution.UpdatePreviews()` select devices using only `IsRunning`. In the
execution-control implementation, pausing sets `IsPaused` but deliberately leaves `IsRunning` true. Consequently,
`Pause()`, `PauseAll()`, and the pause stage of export no longer suppress update calls as the removed
`HardwareUpdateThread` did with its pause wait handle.

This can send data while the UI reports a controller as paused, call `UpdateState` after a module has paused or closed a
resource, and allow another engine frame to overlap the export compiler before `CloseExecution()` takes effect.

Recommended change: schedule only devices for which `IsRunning && !IsPaused`, and cover both controller and preview
paths. Prefer exposing a snapshot of schedulable devices from the output execution manager instead of rediscovering
state by enumerating every configured device.

Evidence:

- `src/Vixen.Core/Sys/Execution.cs:224`
- `src/Vixen.Core/Sys/Execution.cs:238`
- `src/Vixen.Core/Sys/Output/BasicOutputModuleExecutionControl.cs:31`

### High: Starting and stopping a device is not synchronized with the central scheduler

`BasicOutputModuleExecutionControl.Start()` sets `IsRunning = true` before invoking the module's `Start()`. The central
loop enumerates the global controller collection, so it can observe the controller as running and call `Update()` before
hardware initialization finishes. Moving controller instrumentation creation before `Start()` avoids one null access,
but it does not make the module lifecycle safe.

The inverse race exists during stop: the central loop can retain a selected controller while another thread stops its
module. The `_outputDevices` registry in `OutputDeviceExecution<T>` is updated around start/stop, but the central loop
does not use that registry, so it provides no scheduling barrier.

Recommended change: make activation transactional. Finish `Start()` successfully, then publish the device in an
immutable/current active-device snapshot. Remove it from future snapshots before stop, wait for any in-flight frame
using it, and only then invoke `Stop()`. Failed starts must never remain published.

Evidence:

- `src/Vixen.Core/Sys/Output/BasicOutputModuleExecutionControl.cs:12`
- `src/Vixen.Core/Sys/Managers/OutputDeviceExecution.cs:118`
- `src/Vixen.Core/Sys/Execution.cs:224`

### High: Engine shutdown neither cancels nor joins the update loop

`Execution.Shutdown()` only assigns `_executionThread = null`. The running thread exits later when it observes the
state machine as closing, but `StandardClosingBehavior` immediately releases contexts and stops output devices. A frame
can therefore update disposed contexts or call a controller concurrently with its `Stop()` method.

A fast close/reopen can also create a second update thread before the first exits. Both threads would then update engine
state and mutate the shared static `UpdateOutputTasks` list concurrently, defeating the single-scheduler design and
potentially breaking the output barrier.

Recommended change: give the engine loop an explicit cancellation signal and completion handle/task. Shutdown should
signal cancellation and wait for the frame loop, including output tasks, to finish before contexts and devices are
released. Startup should be idempotent and should not publish/start a replacement loop until the prior loop is fully
terminated. Keep the per-frame task collection local rather than static.

Evidence:

- `src/Vixen.Core/Sys/Execution.cs:79`
- `src/Vixen.Core/Sys/Execution.cs:89`
- `src/Vixen.Core/Sys/Execution.cs:137`
- `src/Vixen.Core/Sys/Execution.cs:219`
- `src/Vixen.Core/Sys/State/Execution/Behavior/StandardClosingBehavior.cs:13`

### High: Export closes and disposes running contexts before recording them

`SequenceIntervalGenerator.BeginGeneration()` pauses output devices, calls `Execution.CloseExecution()`, and only then
queries running contexts. Closing execution synchronously invokes `ContextManager.ReleaseContexts()`, which stops,
removes, and disposes every context. `_runningContexts` is therefore empty, and the later resume loop cannot restore
playback that was active when export began.

This also relies on an incidental state-machine interaction: `PauseAll()` changes each output manager to `Paused`, while
`StopAll()` only acts when the manager state is `Started`. Thus close does not actually stop those paused devices; export
later resumes them and reopens execution. The code is fragile even when no sequence is playing.

Recommended change: do not use the application-wide close transition as an export mutex. Add an execution-loop
suspend/quiesce operation that waits for the current frame barrier without releasing contexts or stopping modules.
Capture running contexts before suspension if context pause/resume remains necessary, and restore the exact prior
engine/device/context state in `finally`.

Evidence:

- `src/Vixen.Core/Cache/Sequence/SequenceIntervalGenerator.cs:113`
- `src/Vixen.Core/Cache/Sequence/SequenceIntervalGenerator.cs:114`
- `src/Vixen.Core/Cache/Sequence/SequenceIntervalGenerator.cs:115`
- `src/Vixen.Core/Sys/State/Execution/Behavior/StandardClosingBehavior.cs:13`
- `src/Vixen.Core/Sys/Managers/ContextManager.cs:97`

### High: An unhandled participant failure terminates updates for every device

The central loop has no outer exception boundary. `UpdateOutputDevicesAsync().Wait()` propagates task failures as an
`AggregateException`, and preview dispatch, element update, filter update, or future output-device implementations can
also throw. Because this is now the only engine thread, one exception silently stops all subsequent controller and
preview frames while execution may still report `Open`.

The removed per-device hardware thread contained an exception boundary and isolated failure to one device. The new
architecture needs equivalent isolation plus a top-level fatal guard.

Recommended change: catch and report failures per participant, stop or quarantine the failed device according to a
defined policy, and keep healthy devices running. Add a top-level loop catch/finally that transitions engine state or
raises a visible fault instead of leaving an apparently open but inert engine.

Evidence:

- `src/Vixen.Core/Sys/Execution.cs:137`
- `src/Vixen.Core/Sys/Execution.cs:165`
- `src/Vixen.Core/Sys/Output/OutputPreview.cs:43`

### High: The precision wait busy-spins a high-priority thread for roughly 16 ms per normal frame

For a typical 50 ms interval with little update work, `Sleep()` calls `Thread.Sleep()` for approximately 33 ms and then
busy-spins for the remaining 16 ms. At a 25 ms interval, it can spin for about 16 of every 25 ms; below 20 ms it spins
for the entire remaining interval. The thread also runs at `ThreadPriority.Highest`.

Repeated `Thread.SpinWait(1)` does not yield the scheduler in the way the comment implies. This can consume a material
fraction of a core, reduce battery efficiency, starve UI/thread-pool work on constrained systems, and create the very
scheduling jitter the change is intended to reduce.

Recommended change: schedule against an absolute `Stopwatch` deadline and use an interruptible high-resolution waitable
timer or a short adaptive sleep/yield strategy. If a final spin is measurably necessary, limit it to a very small,
benchmarked sub-millisecond window. Avoid `Highest` priority unless profiling proves it necessary and safe. See
[Implementation handoff: high-precision engine scheduler](#implementation-handoff-high-precision-engine-scheduler) for
the proposed algorithm, Windows APIs, fallback behavior, and acceptance criteria.

Evidence:

- `src/Vixen.Core/Sys/Execution.cs:83`
- `src/Vixen.Core/Sys/Execution.cs:193`
- `src/Vixen.Core/Sys/Execution.cs:200`
- `src/Vixen.Core/Sys/Execution.cs:205`

### Medium: The existing multimedia helper does not improve the execution thread's timer resolution

On Windows Vista and newer, `WindowsMultimedia.BeginEnhancedResolution()` calls
`AvSetMmThreadCharacteristics("Pro Audio")` instead of `timeBeginPeriod`. MMCSS changes scheduling characteristics for
the calling thread; it does not increase the resolution used by `Thread.Sleep`. `StandardOpeningBehavior.Run()` invokes
the helper before `Execution.Startup()` creates the execution thread, so the MMCSS association is applied to the
opening/state-transition thread rather than the engine update thread.

The MMCSS task handle is stored on the temporary `WindowsMultimedia` instance created during opening. Closing constructs
a different instance and calls `EndEnhancedResolution()` with no matching handle, so the original association is not
reverted through that object.

Recommended change: use a high-resolution waitable timer to address wake precision directly. If MMCSS is retained to
reduce scheduling latency, register the actual execution thread after it starts, retain the returned handle for that
thread's lifetime, and revert it in the execution thread's `finally` path. Do not combine MMCSS with
`ThreadPriority.Highest` unless measurements demonstrate a benefit without starving UI and controller work.

Microsoft references:

- [CreateWaitableTimerExW](https://learn.microsoft.com/en-us/windows/win32/api/synchapi/nf-synchapi-createwaitabletimerexw)
- [SetWaitableTimerEx](https://learn.microsoft.com/en-us/windows/win32/api/synchapi/nf-synchapi-setwaitabletimerex)
- [AvSetMmThreadCharacteristicsW](https://learn.microsoft.com/en-us/windows/win32/api/avrt/nf-avrt-avsetmmthreadcharacteristicsw)
- [timeBeginPeriod](https://learn.microsoft.com/en-us/windows/win32/api/timeapi/nf-timeapi-timebeginperiod)

Evidence:

- `src/Vixen.Core/Sys/WindowsMultimedia.cs:26`
- `src/Vixen.Core/Sys/State/Execution/Behavior/StandardOpeningBehavior.cs:8`
- `src/Vixen.Core/Sys/State/Execution/Behavior/StandardOpeningBehavior.cs:23`
- `src/Vixen.Core/Sys/State/Execution/Behavior/StandardClosingBehavior.cs:9`

### Medium: Preview dispatch queues work instead of coalescing to the latest engine state

`Execution.UpdatePreviews()` calls `OutputPreview.Update()`, which calls `PreviewModule.UpdateState()`. That method only
queues `Update` with `BeginInvoke` and returns. The engine can mutate element state for the next frame before the UI
thread consumes the prior request. If the UI is slower than the engine, callbacks can accumulate and several callbacks
can render the same newest state rather than the state associated with their requested frame.

Preview completion does not need to participate in the critical controller barrier. Intermediate preview frames may be
dropped when the UI cannot keep up, provided the next render reflects the most recently published engine state.

Recommended change: publish preview state through a latest-frame mailbox backed by an immutable or safely
double-buffered snapshot. Permit at most one pending UI callback per preview. The callback should render the latest
snapshot available when it runs, then request one more render only if a newer snapshot arrived during rendering. Do not
enqueue one callback per engine frame, and do not await preview rendering from the controller output barrier. Instrument
coalesced-frame count, latest-frame age, render duration, and whether a render request is pending.

Evidence:

- `src/Vixen.Core/Sys/Execution.cs:234`
- `src/Vixen.Core/Sys/Output/OutputPreview.cs:43`
- `src/Vixen.Core/Module/Preview/PreviewModuleInstanceBase.cs:36`

### Medium: The central scheduler ignores active-consumer and device timing contracts

The loop runs whenever execution is open, even if every controller and preview is stopped or paused. This conflicts with
the Jira requirement that the state-update thread consume CPU only when there is an active consumer.

It also schedules every controller at `VixenSystem.DefaultUpdateInterval`, while `IOutputDevice.UpdateInterval` and
`UpdateSignaler` remain exposed but unused. Current built-in descriptors use the default interval, so this may not alter
today's built-in behavior, but the public contract now promises customization that the engine does not honor.

Recommended change: wake/suspend the loop from active-device transitions. Decide whether per-device cadence remains
supported. If it does, use the engine interval as a base tick and dispatch devices only when due; if it does not,
deprecate/remove the obsolete properties in a deliberate compatibility change.

Evidence:

- `src/Vixen.Core/Sys/Execution.cs:137`
- `src/Vixen.Core/Sys/Execution.cs:172`
- `src/Vixen.Core/Sys/Output/IOutputDevice.cs:17`

### Medium: `UpdateAsync` does not provide a true asynchronous parallel controller contract

The new `IOutputDevice.UpdateAsync()` method expands a public interface, but controller and preview implementations only
wrap synchronous work with `await Task.Run(Update)`. The preview implementation is not used by the central loop. The
extra `async`/`await` state machine is unnecessary, cancellation is unavailable, and the contract does not define what
task completion means. It also conflates controllers, whose completion is critical, with previews, whose delivery must
be non-blocking and coalesced.

Recommended change: segregate controller frame consumption from preview publication. Define a documented controller
`UpdateAsync` contract whose task completes only when that controller has finished consuming/sending the current frame.
Pass engine-shutdown cancellation, launch every active controller operation before awaiting any of them, and await the
local task set with `Task.WhenAll`. Controller implementations should use native asynchronous I/O where available.
Adapt legacy synchronous modules with scheduler-owned `Task.Run` work as a transitional path so they still execute in
parallel; do not make every device implementation repeat the same fake-async wrapper. Preview should expose a
non-blocking latest-state publication operation instead of implementing the controller async contract.

Evidence:

- `src/Vixen.Core/Sys/Output/IOutputDevice.cs:12`
- `src/Vixen.Core/Sys/Output/OutputController.cs:131`
- `src/Vixen.Core/Sys/Output/OutputPreview.cs:36`

### Low: Formatting checks fail

`git diff --check` reports trailing whitespace in ten added lines in `Execution.cs`. Several touched files also retain a
missing final newline. These do not affect behavior but should be fixed before merge without reformatting unrelated
code.

## Implementation handoff: high-precision engine scheduler

### Required behavior

The implementation should preserve the main architectural goal of VIX-3750: one engine state update per logical frame,
the same completed state presented to every active controller, true parallel asynchronous controller output, and no next
engine-state update until every controller in the current frame has completed. Timing should remain as close as
practical to the configured 25 ms or 50 ms interval without dedicating a large fraction of a CPU core to spinning.

Preview delivery has a different guarantee. A preview should eventually render the latest published engine state, but
it does not need to render every intermediate frame and must not delay the controller barrier or the next engine frame.
At most one preview render request should be pending at a time.

The scheduler must also:

- start immediately or at an explicitly documented first-frame deadline;
- launch all active controller updates for a frame before awaiting any controller;
- wait for all controller updates to finish before changing engine state for the next frame;
- permit preview rendering to run independently and coalesce to the latest state;
- remain anchored to absolute deadlines so one late frame does not permanently shift the schedule;
- never emit rapid catch-up frames after an overrun;
- expose missed deadlines and lateness through instrumentation;
- be interruptible during every wait and output barrier;
- block without periodic polling when no controller or preview is active; and
- guarantee that only one scheduler instance can run at a time.

### Recommended internal design

Separate deadline calculation from the Windows waiting mechanism so the former can be tested deterministically. An
internal interface such as `IExecutionTimer` is sufficient; it should not become a public module API.

Suggested responsibilities:

- `ExecutionScheduler`: owns frame sequencing, active-consumer state, the parallel controller barrier, cancellation, and
  overrun policy.
- `IExecutionTimer`: exposes the monotonic timestamp/frequency and an interruptible `WaitUntil` operation.
- `WindowsHighResolutionExecutionTimer`: owns the native waitable-timer and stop-event handles and implements
  `IDisposable` or `IAsyncDisposable` as appropriate.
- `IControllerFrameConsumer`: exposes the controller-only asynchronous frame-completion contract.
- `IPreviewFramePublisher`: accepts the latest preview snapshot without waiting for UI rendering.
- `PreviewFrameMailbox`: retains only the latest snapshot and coordinates at most one pending UI callback.
- `ExecutionFrameMetrics`: records scheduled deadline, actual start, update duration, output-barrier duration, sleep
  duration, lateness, interval error, and missed-deadline count.

Keep per-frame task collections local to the scheduler iteration. Do not share a mutable static `List<Task>` across
engine lifetimes.

### Parallel asynchronous controller barrier

Controllers are the critical frame consumers. The engine must keep the current state stable until every active,
non-paused controller has completed its update for that frame. The controller contract should be equivalent to:

```csharp
internal interface IControllerFrameConsumer
{
	Task<ControllerUpdateResult> UpdateAsync(
		long frameId,
		CancellationToken cancellationToken);
}
```

If this or an equivalent contract must be public for module implementers, add complete XML documentation defining:

- that all output/element state for `frameId` remains stable until the returned task completes;
- that completion means command generation and the controller's required send/copy operation have completed;
- whether completion covers operating-system buffer acceptance or physical transmission;
- that implementations must not retain references to mutable frame state after completion;
- how engine-shutdown cancellation is handled; and
- which exceptions represent a failed frame versus a controller that must be stopped or quarantined.

The scheduler should create all controller operations first and await them as a group:

```csharp
var updateTasks = activeControllers
	.Select(controller => UpdateControllerIsolatedAsync(controller, frameId, cancellationToken))
	.ToArray();

PublishLatestPreviewState(previewSnapshot);

var results = await Task.WhenAll(updateTasks).ConfigureAwait(false);
```

Do not place `await controller.UpdateAsync(...)` inside a `foreach`; that serializes controller output. Do not include
preview rendering tasks in `updateTasks`. `UpdateControllerIsolatedAsync` should contain the per-controller exception
boundary and return a result that allows healthy controllers to finish even when one controller fails.

Native asynchronous controller implementations should use APIs such as asynchronous socket, stream, or serial writes
where those APIs accurately represent completion of the required output operation. Controller command preparation can
also be performed concurrently because each controller owns its command buffer. Avoid nested unbounded parallelism:
large-controller command generation should not start another unrestricted `Parallel.For` inside every concurrently
running controller without profiling the combined degree of parallelism.

Some existing controller modules expose only synchronous `Update()`. Preserve true inter-controller parallelism during
migration by placing those calls behind one scheduler-owned legacy adapter that uses `Task.Run`. The adapter should:

- create one task per active legacy controller before the barrier is awaited;
- pass shutdown cancellation only before work starts unless the synchronous module supports safe mid-send
  cancellation;
- catch and report controller-specific failures; and
- be removed controller-by-controller as native async implementations become available.

The extra `async`/`await` wrapper currently present in each output device is not needed for this adapter. Returning the
scheduler-created task directly avoids an additional state machine.

If a controller exceeds the update interval, the scheduler must wait for it, record the frame overrun, and skip missed
deadlines according to the absolute-deadline policy. It must not start the next update for that controller concurrently
and must not issue rapid catch-up frames. A repeated-slow-controller policy may warn, disable, or quarantine the device,
but that policy should be explicit rather than implemented as an arbitrary per-frame cancellation timeout.

### Non-blocking latest-state preview delivery

Preview rendering is intentionally outside the controller barrier. After the engine completes its state calculation,
publish a read-only `PreviewFrameSnapshot` (or equivalent double-buffered state) to each active, non-paused preview. The
snapshot must remain safe to read while the engine computes later frames.

`PreviewFrameMailbox.Publish` should atomically replace the previous snapshot. It should post to the UI thread only when
no render callback is already pending. The UI callback should:

1. Read the latest published snapshot when it begins.
2. Render that snapshot.
3. Clear the pending marker.
4. If a newer snapshot arrived during rendering, arrange exactly one additional callback or immediately render the
   latest snapshot, depending on UI responsiveness requirements.

This design intentionally coalesces intermediate frames. If ten engine frames arrive while the UI is blocked, the
preview should not accumulate ten callbacks; after the UI becomes available it should render the newest available state.
A blocked or failing preview must never hold up `Task.WhenAll` for controllers or prevent the next engine frame.

If creating a full immutable preview snapshot is too expensive, use a double-buffered published-state model with clear
ownership or reference counting. Do not allow the UI to enumerate mutable engine collections while the scheduler is
writing the next frame.

### Absolute-deadline algorithm

Use `Stopwatch.GetTimestamp()` as the monotonic clock. Convert the configured interval to stopwatch ticks once whenever
the interval changes:

```csharp
long intervalTicks = checked((long)Math.Round(
	interval.TotalSeconds * Stopwatch.Frequency,
	MidpointRounding.AwayFromZero));
```

The scheduling policy should be equivalent to:

```text
scheduledFrameStart = timestamp now

while not cancelled:
    wait until at least one active consumer exists, or cancellation
    execute exactly one frame for scheduledFrameStart
    scheduledFrameStart += intervalTicks

    now = monotonic timestamp
    if now >= scheduledFrameStart:
        missed = ((now - scheduledFrameStart) / intervalTicks) + 1
        record missed deadlines and current lateness
        scheduledFrameStart += missed * intervalTicks
        continue without emitting catch-up frames

    wait interruptibly until scheduledFrameStart
```

When the scheduler wakes after having no active consumers, reset `scheduledFrameStart` from the current timestamp. Idle
time must not be interpreted as thousands of missed frames. If changing `DefaultUpdateInterval` while running remains
supported, apply the new interval at a frame boundary and reset the next deadline deliberately.

The frame operation should complete these stages in order:

1. Snapshot active, non-paused controllers and previews separately.
2. Update contexts, elements, and filters once.
3. Publish or retain a stable logical controller frame and a safely readable preview snapshot for that iteration.
4. Create/start every controller update task without awaiting an individual controller.
5. Publish the preview snapshot to the non-blocking latest-state mailboxes.
6. Await `Task.WhenAll` for controller tasks, isolating participant failures.
7. Record metrics and calculate the next wait.

### Windows wait implementation

The preferred implementation for supported Windows versions is an auto-reset waitable timer created with
`CreateWaitableTimerExW` and `CREATE_WAITABLE_TIMER_HIGH_RESOLUTION` (`0x00000002`). Microsoft documents this flag for
time-critical situations where delays of several milliseconds are unacceptable; it is supported on Windows 10 version
1803 and newer.

Implementation requirements:

- Wrap native timer/event handles in `SafeWaitHandle`-based owners so all success and failure paths release them.
- Request only the timer access rights needed to set and wait on the timer.
- Program a one-shot relative due time in 100-nanosecond units for each deadline.
- Wait on both the timer handle and the scheduler stop/wake handle so shutdown and active-consumer changes are
  immediate.
- Check every native return value and include `Marshal.GetLastWin32Error()` in structured error logging.
- Fall back cleanly if high-resolution timer creation is unavailable or fails.

Do not set a native periodic timer and assume it represents completed frames. Frame work can exceed the interval, and
the engine must apply the explicit missed-deadline policy rather than allowing timer signals to queue or collapse
implicitly.

### Bounded final spin

A final spin may be retained, but it must be a small and measurable optimization rather than the primary wait. Start
with a maximum budget of 250-500 microseconds:

1. If the remaining duration exceeds the spin budget, arm the high-resolution timer for
   `deadline - spinBudget` and wait interruptibly.
2. After wake, compare `Stopwatch.GetTimestamp()` with the absolute deadline.
3. Spin only while early and only until the absolute deadline or cancellation.
4. If the wait returns late, record lateness and do not spin.

At a 500 microsecond maximum, spin overhead is bounded to approximately 1% of one core at 50 ms and 2% at 25 ms,
before accounting for frames that wake late and do not spin. The exact budget should be tuned from p99 wake-error data;
it should not be expanded back to a fixed 16 ms safety margin.

Repeated `Thread.SpinWait(1)` calls do not progressively yield because each call starts a new one-iteration spin. If the
implementation uses `SpinWait`, retain a `SpinWait` instance or use a bounded timestamp loop, and validate behavior on
single-core/limited-CPU test configurations.

### Fallback behavior

If `CREATE_WAITABLE_TIMER_HIGH_RESOLUTION` is unavailable, use a clearly instrumented fallback rather than silently
returning to a 16 ms spin:

- pair `timeBeginPeriod(1)` with `timeEndPeriod(1)` for the exact scheduler lifetime;
- perform a coarse interruptible wait until near the deadline;
- apply the same bounded final-spin limit; and
- log/measure that the fallback path is active.

On Windows 10 version 2004 and newer, `timeBeginPeriod` primarily affects the calling process. Windows 11 can withdraw
the higher-resolution guarantee when a window-owning process is fully occluded, minimized, invisible, and inaudible.
The fallback therefore remains best-effort and must not be treated as a real-time guarantee.

### MMCSS and thread priority

MMCSS and timer resolution solve different problems. A high-resolution timer improves when a wait becomes eligible to
complete; MMCSS can improve when the scheduler actually runs after becoming eligible.

If MMCSS is used:

- call `AvSetMmThreadCharacteristics("Pro Audio", ...)` from inside the execution thread;
- retain the returned handle in that thread's scheduler state;
- call `AvRevertMmThreadCharacteristics` from the scheduler's `finally` path;
- log registration/reversion failures; and
- begin with normal thread priority, adding `AvSetMmThreadPriority` or managed priority changes only after comparative
  measurements.

Do not register the UI/opening thread and do not create a second helper instance during shutdown to perform the revert.

### Suggested initial acceptance criteria

These are engineering targets to validate and adjust from measurements, not hard real-time guarantees:

- At 25 ms and 50 ms, representative preview-only and controller workloads show p99 frame-start lateness no greater
  than 1 ms on supported Windows 10/11 hardware under normal load.
- Maximum final-spin time is 500 microseconds or less per frame, verified through instrumentation.
- Scheduler waiting/spinning adds no more than approximately 2% of one logical core at 25 ms and 1% at 50 ms when frame
  work itself is excluded.
- Frame deadlines remain anchored after an isolated late frame; no catch-up burst occurs.
- Every missed deadline is counted and reported, including the number of nominal intervals missed.
- Stop, pause, export quiesce, and loss of the last active consumer interrupt the timer wait promptly.
- Every active controller is dispatched before the controller barrier is awaited; controller updates are observably
  concurrent rather than serialized.
- The scheduler never overlaps two updates for the same controller and never runs two engine loops concurrently.
- Preview rendering is excluded from the controller barrier, never has more than one UI callback pending per preview,
  and renders the newest state after intermediate frames are coalesced.
- Timer-handle, stop-handle, and MMCSS lifetime tests show no leaked native resources across repeated open/close cycles.

If the 1 ms p99 target is not achievable on representative hardware, record the measured distribution and choose the
smallest spin budget that produces a meaningful improvement. Do not increase CPU use based only on one machine's
maximum outlier.

## Suggested validation before merge

Add deterministic tests around an injectable clock/wait strategy and fake output devices:

1. A paused controller and paused preview receive zero updates until resumed.
2. A device cannot be updated before `Start()` completes or after `Stop()` begins.
3. Closing waits for an in-flight slow update; rapid close/open never creates overlapping loops.
4. One throwing controller or preview is isolated while healthy devices continue receiving frames.
5. Export during playback preserves and resumes the original contexts and exact device pause/running state.
6. Two controllers observe the same monotonically increasing frame ID and neither receives the next frame before both
   complete the current frame.
7. A gate-based concurrency test blocks every fake controller until all controllers have entered `UpdateAsync`, proving
   that updates were launched in parallel rather than sequentially.
8. A fast controller may complete before a slow controller, but the next engine-state update does not begin until both
   have completed.
9. An over-budget controller produces explicit missed-deadline metrics without concurrent updates of that controller.
10. A blocked preview does not delay controller completion or the next engine frame.
11. Publishing many frames while the preview UI thread is blocked creates at most one pending callback; after release,
    the preview renders the newest frame ID rather than replaying every intermediate frame.
12. A preview frame published during rendering results in at most one follow-up render and is not lost indefinitely.
13. With no active consumers, the loop blocks and the refresh counter does not increment.
14. Timing measurements cover median, p95, p99, maximum interval, missed deadlines, and CPU usage for preview-only, one
   controller, multiple controllers, and deliberately slow-controller scenarios.
15. Absolute-deadline unit tests prove that one late frame does not shift later deadlines and that overruns skip missed
    deadlines without emitting catch-up frames.
16. An idle-to-active transition resets the schedule without reporting idle time as missed frames.
17. Changing the interval at a frame boundary produces the documented next deadline.
18. Windows integration tests exercise the high-resolution timer and forced fallback paths, cancellation during a wait,
    repeated native-handle creation/disposal, and MMCSS registration/reversion on the execution thread.
19. A performance harness runs both 25 ms and 50 ms configurations and reports frame-start lateness, interval error,
    final-spin duration, update duration, output-barrier duration, CPU use, and missed deadlines.

## Verification performed

- Full `Vixen_Tests` Release/x64 target built successfully with MSBuild.
- `dotnet test --no-build --no-restore` passed all 490 tests.
- No test files are changed in the reviewed commit range.
- Build emitted existing `NU1904` warnings for LiteDB 4.1.4; this is unrelated to VIX-3750.
