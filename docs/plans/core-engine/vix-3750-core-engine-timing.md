# Make core-engine frame scheduling safe, bounded, and observable (VIX-3750)

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds. Maintain this document in accordance with `.agents/PLANS.md` from the repository root.

This plan turns the findings in `docs/reviews/vix-3750-core-engine-timing-review.md` into the implementation of VIX-3750. It is self-contained: the review is useful evidence, but this plan contains the requirements and implementation direction that must be followed.

## Purpose / Big Picture

Vixen's execution engine advances element state and sends it to lighting controllers. At present, the central loop can update paused devices, race a device's start or stop operation, keep running after shutdown, and busy-spin for approximately sixteen milliseconds of each frame. Export also closes the whole execution engine to obtain exclusive state, which disposes playback contexts before their state can be recovered.

After this work, exactly one cancellable scheduler advances engine state once per logical frame. It provides the same completed state to every active controller, starts all controller sends in parallel, and does not change state for the following frame until the current controller sends have completed. Previews receive safely published latest-state updates but never delay a controller. Pausing, stopping, export, shutdown, and idle operation promptly interrupt waiting. The instrumentation display exposes frame lateness, missed deadlines, output-barrier time, bounded spin time, and preview coalescing so timing behavior can be measured rather than guessed.

An operator can verify the result by playing a sequence with controllers and previews, pausing an individual device, exporting during playback, reopening execution repeatedly, and inspecting the instrumentation display. Automated deterministic tests prove lifecycle and frame-order guarantees; a Windows performance harness records timing distributions at both 25 ms and 50 ms.

## Progress

- [x] (2026-08-25) Read `.agents/PLANS.md`, `docs/reviews/vix-3750-core-engine-timing-review.md`, and the current execution, output-device, export, context, lifecycle, and instrumentation code.
- [x] (2026-08-25) Resolved product decisions with the requester: per-device cadence is deprecated; controller failures retry at most five times; export owns a quiesced state and restores prior running/paused state; MMCSS is deferred until measurements justify it.
- [x] (2026-08-25) Milestone 1: Added the internal scheduler, injectable timer abstraction, absolute-deadline calculator, Windows high-resolution/fallback timer, frame metrics/instrumentation values, and 13 deterministic tests. The focused test command passed 13/13 and the full `dotnet test src/Vixen.Tests/Vixen.Tests.csproj --no-build --no-restore` suite passed 503/503. Existing unrelated build warnings remain, including LiteDB `NU1904`.
- [x] (2026-08-25) Milestone 2: Routed controller and preview lifecycle through immutable leased active snapshots. Device start/resume publishes only after its module operation succeeds; pause/stop unpublishes and waits for current frame leases before invoking the module. The execution loop now starts all active controller sends concurrently, waits at one barrier, isolates failures, and removes a controller after five consecutive failed frames. Removed the unused POC-only `IOutputDevice.UpdateAsync()` API and its built-in implementations. User confirmed the full tests pass without a Rider console window; `dotnet build src/Vixen.Core/Vixen.Core.csproj --no-restore` also passed with three existing unrelated warnings.
- [x] (2026-08-25) Milestone 3: Implemented coalesced live-state preview publication with per-preview mailboxes, invalidation, and deterministic mailbox tests. The requester confirmed all tests pass.
- [x] (2026-08-25) Milestone 4: Replaced export's close/reopen path with a scheduler quiesce lease, captured and restored device/context running-paused behavior, and serialized output lifecycle operations behind the lease. Opening no longer changes multimedia resolution and closing already joins the scheduler before it releases contexts or devices. MSBuild built `Vixen.Tests`; focused scheduler tests passed 6/6 and `git diff --check` passed. A full CLI test invocation was stopped after its existing test host stalled without reporting failures.
- [ ] Milestone 5: Deprecated the per-device cadence and signaler compatibility APIs, removed unused legacy device-timing instrumentation, and registered scheduler timing, consumer, failure, preview-coalescing, and timer-fallback values with the instrumentation screen. Focused engine tests passed 19/19. The requester reported a 50 ms preview-only baseline using the high-resolution timer: frame lateness from near zero to 15 ms (30% of the cadence); interval error from -5 ms to +15 ms; frame-update duration 10–20 ms, mostly context update at about 14 ms; preview frame age under 1 ms; output barrier 0.001 ms; and sleep duration 15–30 ms. Remaining: run/record 25 ms and 50 ms controller and slow-controller scenarios.
- [ ] Milestone 6: Apply required project skills, run full validation, manually verify, and update this plan's outcome sections.

## Surprises & Discoveries

- Observation: `OutputDeviceExecution<T>` already keeps a private dictionary of started devices, but `Execution.UpdateState` ignores it and instead enumerates all configured controllers and previews by `IsRunning`.
  Evidence: `src/Vixen.Core/Sys/Managers/OutputDeviceExecution.cs` updates `_outputDevices` around `Start`/`Stop`; `src/Vixen.Core/Sys/Execution.cs` currently uses `VixenSystem.OutputControllers.Where(c => c.IsRunning)` and `VixenSystem.Previews.Where(p => p.IsRunning)`.

- Observation: the initial POC's `IOutputDevice.UpdateAsync()` methods were only `Task.Run(Update)` wrappers, and no production call site used them.
  Evidence: repository-wide search found only the interface declaration and implementations on `OutputController`, `SmartOutputController`, and `OutputPreview`; the scheduler dispatches synchronous `UpdateFrame()` work directly.

- Observation: `Vixen.Core` already grants `InternalsVisibleTo` to `Vixen.Tests`, allowing focused tests for the internal scheduler without making timing implementation types public module APIs.
  Evidence: `src/Vixen.Core/Vixen.Core.csproj` contains `<InternalsVisibleTo Include="Vixen.Tests" />`.

- Observation: C# cannot define the `Vixen.Sys.Execution` namespace while `Vixen.Sys.Execution` is already a type.
  Evidence: the first Milestone 1 build failed with `CS0101: The namespace 'Vixen.Sys' already contains a definition for 'Execution'`.

- Observation: an active-snapshot change must wake the scheduler only after releasing the device manager lock. The scheduler refreshes its active-consumer predicate while handling the notification, which also acquires the manager lock.
  Evidence: `OutputDeviceExecution<T>` changes `_activeDevices` under `_syncRoot` and then calls `Execution.NotifyActiveConsumerStateChanged()` before waiting for outstanding leases.

- Observation: the Visual Studio MSBuild toolchain builds the test project successfully, whereas a normal `dotnet test` build attempts unavailable native C++ project targets in this environment.
  Evidence: `msbuild src/Vixen.Tests/Vixen.Tests.csproj /m /t:Build /p:Configuration=Debug /p:Platform=x64 /clp:ErrorsOnly` completed successfully; the focused `dotnet test --no-build` run passed 6/6.

- Observation: the output lifecycle quiesce test must dispose its writer lease before awaiting the task that is deliberately blocked on its reader scope.
  Evidence: awaiting the lifecycle task while retaining the lease caused the test to wait indefinitely; releasing the lease first made the individual test pass in 243 ms and the focused suite pass 6/6.

- Observation: the in-flight-frame quiesce test needs to wait until the scheduler has recorded the quiesce request before it releases the frame.
  Evidence: the former `Task.Yield()` only yielded test scheduling and did not prove `ExecutionScheduler.Quiesce()` had incremented its request count. The test now waits for the internal `IsQuiesced` state and passed individually in 35 ms and as part of the focused suite (6/6).

- Observation: legacy output-device timing instrumentation classes had no references outside their own declarations.
  Evidence: repository search found no consumers of `OutputDeviceRefreshRateValue`, `OutputDeviceSleepTimeActualValue`, `OutputDeviceSleepTimeRequestedValue`, or `OutputDeviceUpdateTimeValue`; controller work-duration instrumentation remains in use and is not cadence control.

- Observation: with no controllers and one preview at 50 ms, preview dispatch stays current while normal engine work consumes a material portion of the frame budget even with the high-resolution timer.
  Evidence: requester-reported instrumentation shows `Execution timer fallback active = 0`, frame-update duration of 10–20 ms with context update contributing about 14 ms, preview frame age below 1 ms, and output barrier duration of 0.001 ms, alongside frame lateness up to 15 ms (30% of the cadence), interval error from -5 ms to +15 ms, and scheduler sleep from 15 ms to 30 ms. Frame work plus sleep therefore spans 25–50 ms before wake lateness.

## Decision Log

- Decision: The scheduler has one global cadence: `VixenSystem.DefaultUpdateInterval`. Mark per-device `IOutputDevice.UpdateInterval` and `IOutputDevice.UpdateSignaler` obsolete, retain compatible implementations for this release, and do not dispatch a different cadence per device.
  Rationale: The requester explicitly ended support for per-device timing. Retaining the members temporarily prevents an unnecessary module compatibility break while warning future callers that the central scheduler ignores them.
  Date/Author: 2026-08-25 / requester and planning agent.

- Decision: A controller failure is isolated from healthy controllers. It remains eligible for the next frame after logging the failure, but its execution control stops and removes it from active snapshots after five consecutive failed frames; a successful frame resets its consecutive-failure count. There is no automatic slow-controller threshold.
  Rationale: The requested policy is best-effort operation with at most five retries. A slow controller must still complete the barrier so the frame state stays valid; its lateness is measured rather than cancelled or disabled by an arbitrary timeout.
  Date/Author: 2026-08-25 / requester and planning agent.

- Decision: Export uses scheduler quiescence, not `Execution.CloseExecution()`. Quiescence freezes execution after the current controller barrier and prevents every non-export scheduler frame until released. Export captures output-device running/paused state and the state of existing contexts before creating its compiler context; in `finally`, it releases the compiler context, restores the captured context and device states, and releases the scheduler. The original context instances may be retained when possible but identity is not a contractual requirement; restoration is by prior behavior (running or paused), and export is the only permitted state mutator while quiesced.
  Rationale: The requester requires export to own state completely yet restore prior running/paused behavior. This avoids disposing playback contexts as the current close/reopen path does and guarantees outside playback cannot race the compiler.
  Date/Author: 2026-08-25 / requester and planning agent.

- Decision: Implement high-resolution waitable-timer scheduling and bounded final spin now, but do not register MMCSS or set high thread priority. Add MMCSS only in a later, measured change if the performance harness proves it is necessary for the agreed timing target.
  Rationale: MMCSS affects scheduling eligibility, not timer precision; the prior helper registers the wrong thread and cannot reliably revert it. The requester explicitly deferred it pending measurements.
  Date/Author: 2026-08-25 / requester and planning agent.

- Decision: Remove the initial POC's public `IOutputDevice.UpdateAsync()` member and its built-in implementations. The execution engine directly creates one task per selected synchronous controller and waits for their common frame barrier.
  Rationale: The requester confirmed there are no external consumers, and the member only wrapped synchronous work in `Task.Run`. Removing it avoids retaining a misleading asynchronous API while preserving concurrent controller dispatch where it belongs.
  Date/Author: 2026-08-25 / requester and implementation session.

- Decision: Keep the planned `src/Vixen.Core/Sys/Execution/` directory, but use namespace `Vixen.Sys.Engine` for its types.
  Rationale: `Vixen.Sys.Execution` is already the existing static engine facade type. C# rejects a child namespace with the same qualified name, while `Vixen.Sys.Engine` clearly identifies the internal scheduling implementation and avoids a public API rename.
  Date/Author: 2026-08-25 / implementation session.

- Decision: Preview frame publication carries only an immutable frame ID and publication timestamp. The preview UI renders the live `VixenSystem.Elements` state when its coalesced callback executes; it does not receive a copied element-state collection.
  Rationale: The requester selected live reads from the element state latched by the engine. Current GDI and OpenGL preview implementations already read `VixenSystem.Elements` during `UpdatePreview`, so a collection copy would add allocation and a competing state-ownership model without changing their render input.
  Date/Author: 2026-08-25 / requester and implementation session.

- Decision: Export holds a writer lease on a process-wide output lifecycle gate. Each normal output-device lifecycle operation takes a reader scope, so it blocks while export is quiesced; operations initiated by the export thread are allowed through the reentrant reader scope.
  Rationale: This uses the existing `OutputDeviceExecution<T>` lifecycle funnel rather than adding competing state flags to every manager. It makes export the only output state mutator while it owns the scheduler quiesce lease.
  Date/Author: 2026-08-25 / implementation session.

- Decision: Retain the obsolete interval and signaler members for one compatibility release, but expose scheduler measurements as milliseconds and count values in the existing instrumentation screen.
  Rationale: Existing modules and serialized configuration retain binary/source compatibility while new callers are directed to the single global scheduler. Converting stopwatch ticks at the instrumentation boundary makes timing data directly interpretable by operators.
  Date/Author: 2026-08-25 / implementation session.

## Outcomes & Retrospective

Milestones 1 and 2 establish the timing foundation and route live controller output through it. The new `ExecutionScheduler` uses an injected active-consumer predicate and frame delegate, supports immediate idle-to-active frames, absolute deadlines, interval-boundary resets, cancellation/wake, a 500-microsecond maximum final spin, and an internal metrics set. Active-device snapshots now hold frame leases, so pause and stop first prevent future dispatch and then wait for selected work to finish. The execution loop directly launches every selected controller's synchronous frame update before waiting for the common barrier; failures are isolated for the five-consecutive-failure policy. The unused POC `IOutputDevice.UpdateAsync()` API was removed. `WindowsHighResolutionExecutionTimer` requests only timer modify/synchronize access and falls back to process-scoped one-millisecond timer resolution when high-resolution timer creation is unavailable. The focused Milestone 1 suite passed 13/13; the requester confirmed the full suite passed after the Rider-console test adjustment. At full completion, record measured 25 ms and 50 ms results, final test totals, any timer fallback observations, deviations from this plan, and whether the data supports a follow-up MMCSS experiment.

## Context and Orientation

`src/Vixen.Core/Sys/Execution.cs` is the current static entry point for the engine loop. `Execution.Startup()` creates a background thread and `Execution.Shutdown()` merely clears its field; the loop updates contexts, elements, filters, previews, and controllers. A *frame* in this plan is one complete iteration: calculate the current engine state, publish it, start all controller output work, wait for every selected controller to complete, and then schedule the next state calculation.

`src/Vixen.Core/Sys/Managers/OutputDeviceExecution.cs` coordinates `Start`, `Stop`, `Pause`, and `Resume` for either `OutputController` or `OutputPreview`. Its active-device dictionary is currently private and not a scheduling authority. `BasicOutputModuleExecutionControl` presently exposes `IsRunning` before module `Start()` completes, so it cannot alone make lifecycle publication safe. `OutputDeviceCollectionExecutionMediator`, `OutputControllerManager`, and `PreviewManager` forward the public manager calls to that execution component.

Controllers in `src/Vixen.Core/Sys/Output/OutputController.cs` build command buffers and call their module's `UpdateState`. A controller is *active* only after its module has started successfully and while it is not paused. A controller *barrier* is the `Task.WhenAll` wait for the immutable active-controller snapshot selected for the current frame. Removing a controller must first prevent future selection, then wait for any frame that selected it, and only then call its module's `Stop()`.

Previews in `src/Vixen.Core/Sys/Output/OutputPreview.cs` call `IPreview.UpdateState`, currently implemented by `src/Vixen.Core/Module/Preview/PreviewModuleInstanceBase.cs` by queueing a UI callback for every call. A *latest-state mailbox* replaces that queueing behavior: it stores only the newest safe preview snapshot and schedules at most one UI callback per preview. The mailbox callback reads and renders the newest snapshot when it runs, so intermediate frames can be intentionally dropped.

`src/Vixen.Core/Cache/Sequence/SequenceIntervalGenerator.cs` drives offline sequence export. Today it pauses outputs, calls `Execution.CloseExecution()`, and only then looks for running contexts; closing releases those contexts in `StandardClosingBehavior` and `ContextManager.ReleaseContexts`. This plan replaces close/reopen with a quiesce lease that makes the scheduler idle after an in-flight frame finishes.

`VixenSystem.DefaultUpdateInterval` is the one retained update interval. It is changed through `src/Vixen.Core/Sys/VixenSystem.cs`; the scheduler detects the value at a frame boundary, converts it once to monotonic clock ticks, and deliberately resets the next deadline. `IOutputDevice.UpdateInterval` and `UpdateSignaler` remain only as obsolete compatibility members and are never read by the scheduler.

The engine uses existing `Vixen.Instrumentation` values, registered by `Execution.InitInstrumentation()`. Add new execution-specific value types under `src/Vixen.Core/Sys/Instrumentation/` so the existing instrumentation consumers can display them. A performance harness may write a concise CSV or JSON artifact under a new ignored/binary-output path only if project conventions permit it; its essential summary must also be logged so it is visible without an artifact viewer.

## Plan of Work

### Milestone 1 — Deterministic scheduler and high-precision timing foundation

Create internal, testable scheduler components under `src/Vixen.Core/Sys/Execution/` (create the directory if it does not exist). `ExecutionScheduler` owns the single loop lifecycle, cancellation, active-consumer wake signal, frame IDs, absolute-deadline calculation, quiesce leases, and the controller barrier. `IExecutionTimer` supplies a monotonic timestamp, its frequency, and an interruptible wait until a deadline; its test double advances time deterministically without sleeping. Keep all these types `internal` because `Vixen.Core` already allows its tests access.

Create `WindowsHighResolutionExecutionTimer` in the same area. It owns the Windows one-shot waitable timer and a stop/wake event through safe-handle-based disposable owners. It uses `CreateWaitableTimerExW` with `CREATE_WAITABLE_TIMER_HIGH_RESOLUTION` (`0x00000002`) on supported Windows, programs relative due times in 100-nanosecond units, and waits for either deadline or cancellation/wake. Validate every native return value, log `Marshal.GetLastWin32Error()` when failing, and dispose every created handle even when initialization fails. When high-resolution creation fails, use a clearly logged fallback that calls `timeBeginPeriod(1)` only for the scheduler lifetime, pairs it with `timeEndPeriod(1)` in disposal, performs an interruptible coarse wait, and has the same bounded final spin. Delete `WindowsMultimedia` use from opening/closing once its responsibilities are superseded; do not retain its unmatched MMCSS calls.

Implement deadline calculation entirely against `Stopwatch.GetTimestamp()` units. Start the first frame immediately when the first active consumer appears. After each completed frame, add the interval ticks to the prior scheduled deadline, not to the actual completion time. If current time has reached or passed the next deadline, calculate and record all missed nominal intervals, advance the deadline past them, and run no catch-up frames. When the scheduler transitions from no active consumer to any active controller or preview, reset the first deadline from the current timestamp so idle time never becomes missed frames. When the global interval changes, apply it only after a completed frame and reset the next deadline from then-current time.

Use a final spin budget of 500 microseconds maximum. For a deadline farther away, arm the timer for deadline minus the budget; after an early wake, spin only until the absolute deadline or cancellation. Never spin after a late wake. Keep the execution thread at normal priority. Do not introduce MMCSS, `ThreadPriority.Highest`, a periodic native timer, or a fixed sixteen-millisecond spin margin.

Add `ExecutionFrameMetrics` and concrete instrumentation values for scheduled deadline, frame-start lateness, frame-update duration, controller-barrier duration, timer/sleep duration, final-spin duration, interval error, missed-deadline count, active-controller count, active-preview count, controller-failure count, and preview coalesced-frame count/latest-frame age. Values that are running totals must be explicit `CountValue` implementations; current-frame durations use `DoubleValue`/milliseconds format. Ensure timer fallback usage is visible in logs and instrumentation.

Add focused tests under `src/Vixen.Tests/Sys/Execution/` using a fake timer and fakes for frame work. Cover immediate first frame, absolute deadlines after an isolated late frame, overrun missed-deadline counting without catch-up, idle-to-active reset, interval change at a boundary, cancellation during a wait, and no refresh increment while idle. Include tests that inspect the metrics values, not wall-clock timing. Before this milestone ends, read and apply `.agents/skills/dotnet-best-practices/SKILL.md` because this adds C# implementation code; record relevant findings in the Decision Log.

Acceptance: deterministic tests prove the stated deadline policy with no `Thread.Sleep`, and a Windows smoke test can create and dispose both high-resolution and forced-fallback timers without leaking handles.

### Milestone 2 — Lifecycle-safe active snapshots and controller barrier

Extend `IOutputDeviceExecution<T>` and `OutputDeviceExecution<T>` with an internal immutable snapshot API for scheduler consumption and with synchronization operations necessary to publish/unpublish devices. The snapshot contains only devices whose module `Start()` completed successfully and which are running and not paused. It must be safe to enumerate without holding the manager lock. Any state transition that changes schedulability signals the scheduler wake event so an idle scheduler, paused device, or stop request reacts immediately.

Make start transactional in `BasicOutputModuleExecutionControl` and `OutputDeviceExecution<T>`: call module `Start()` first; only on success set running/not-paused state and publish the device to the active immutable snapshot. If it throws, log the failure, leave it unpublished and not running, and do not create controller instrumentation that suggests a healthy device. For stop, atomically remove the device from future snapshots first, signal scheduler wake, wait until the scheduler reports no in-flight frame holds that device, then invoke module `Stop()` and clear lifecycle state. Pause removes the device from future snapshots and waits for any selected frame to finish before invoking module `Pause`; resume invokes module `Resume()` successfully before republishing it. Apply the same lifecycle guarantees to previews, although preview callbacks remain non-blocking and must be invalidated or ignored after stop.

The execution loop creates one `Task.Run` per active `OutputController` before awaiting their common frame barrier. Each task completes only after command generation and the controller module's current synchronous `UpdateState` call have returned. It may not assume a synchronous module can be cancelled mid-send, and it may not await inside a controller `foreach`. The initial POC's public `IOutputDevice.UpdateAsync()` member is removed rather than reused.

In `ExecutionScheduler`, snapshot active controllers and previews separately before calculating engine state. Update contexts, elements, and filters exactly once. Filters run only when at least one controller is active, matching the present behavior. Give every frame a monotonically increasing ID. Start every controller operation from the snapshot, publish the preview frame through Milestone 3's non-blocking path, and await the local `Task[]` with `Task.WhenAll`. Per-controller exception handling returns a result rather than allowing an exception to end the scheduler; it logs controller ID/name/frame ID, increments consecutive failures, and permits retry. On the fifth consecutive failure, request the output manager to stop and unpublish that controller; healthy controllers still complete their frame and continue. Reset the count after a successful update. A top-level `try`/`catch`/`finally` logs fatal scheduler faults, exposes a visible execution fault/state transition rather than leaving execution marked open, completes waiters, and releases timer resources.

Replace `Execution`'s static mutable `UpdateOutputTasks` list with local task arrays owned by one scheduler frame. Have `Execution.Startup()` create a scheduler only when no prior instance is running or terminating. Have `Execution.Shutdown()` request cancellation, wake the timer, and join the scheduler including its output barrier before returning. It must be safe to call startup/shutdown repeatedly and must never create two loops. `StandardClosingBehavior.Run` must rely on that join before releasing contexts and stopping devices.

Add tests for paused controller exclusion, start not being observable before completion, stop/pause waiting for a gated frame, failed start remaining unpublished, five consecutive failures removing only that controller, recovery resetting the failure count, a throwing controller not stopping healthy output, a gated multi-controller test proving all controller tasks enter before the barrier waits, same frame ID across controllers, no next state update until slow and fast current-frame controllers finish, close waiting for in-flight output, and rapid close/open never overlapping schedulers. Use `TaskCompletionSource` gates and fake consumers, not sleep-based assertions.

Acceptance: all controller lifecycle and barrier tests pass; manually pausing a controller produces no sends until resume; repeated close/open does not produce duplicate updates or calls into stopped hardware.

### Milestone 3 — Non-blocking, coalesced preview publication

Define internal `IPreviewFramePublisher`, `PreviewFrameSnapshot`, and `PreviewFrameMailbox` under `src/Vixen.Core/Sys/Execution/`. A snapshot must be immutable to its reader or backed by a double-buffered ownership model so the UI never enumerates mutable engine collections while the scheduler calculates the next frame. Research the actual data accessed by each current preview module before selecting the least-allocating safe representation; document that representation and ownership in this plan's Decision Log before implementation. Do not copy unsafe references merely to avoid allocation.

Replace `OutputPreview.Update()`'s per-frame `PreviewModule.UpdateState()` dispatch with mailbox publication. Each active, non-paused preview receives the newest snapshot after controller tasks are launched but before the barrier is awaited. `Publish` atomically replaces any unrendered snapshot, increments the coalesced count when replacing one, and posts to the UI only when no callback is pending. The UI callback reads the newest snapshot, renders it, clears its pending flag, and schedules exactly one follow-up callback if a newer frame appeared while rendering. A preview error is logged and isolated; it never faults the scheduler or holds the controller barrier. Stop/pause invalidates queued work so a callback cannot render an obsolete device after lifecycle transition.

Refactor `PreviewModuleInstanceBase` and all affected preview contracts only as required to accept the stable snapshot; if this changes any public or protected C# member, read `.agents/skills/csharp-docs/SKILL.md` first and update complete XML documentation in the same change. The documentation must state snapshot ownership and that preview delivery is best effort and may coalesce frames.

Add tests for paused preview exclusion, a blocked preview not delaying a next controller frame, many publications while its UI dispatcher is blocked yielding one pending callback and the latest frame ID after release, a frame arriving during render yielding at most one follow-up, failed preview isolation, and queued-work invalidation after stop. Include instrumentation assertions for coalesced count, pending state, frame age, and render duration.

Acceptance: a deliberately blocked preview neither lengthens controller-barrier time nor creates a callback per engine frame, and it renders the newest state after being released.

### Milestone 4 — Closing, export quiescence, and state restoration

Add an internal scheduler quiesce lease exposed through `Execution`. Acquiring it requests that no new ordinary frame start, wakes any timer wait, and waits for the current frame's controller barrier to finish. While a lease is held, external device lifecycle actions must either be rejected/serialized behind the lease or have no effect until it is released; choose the existing manager synchronization path that provides this guarantee and record it in the Decision Log. Releasing the final lease wakes the scheduler and resets its deadline. Shutdown takes precedence over quiescence and completes all waiters with a clear cancelled/closed result.

Change `SequenceIntervalGenerator.BeginGeneration()` to acquire this lease before it changes context, element, filter, or output state. Capture each output device's prior running/paused state and capture whether every existing non-export context was running or paused before pausing those contexts. Do not call `Execution.CloseExecution()` and do not dispose normal contexts. Then clear state, create/start the compiler context, and generate export frames. `EndGeneration()` must use `finally`-safe restoration: stop/release only the compiler context, restore captured context and device behavior in a defined order, update filters as necessary, and release the quiesce lease even if compilation fails. Do not call `OpenExecution()` as a blind recovery action: restore the prior execution-open state only if it was open before export. If a context cannot be resumed, log it as an export restoration failure and continue restoring all other recorded state.

Make `StandardOpeningBehavior` start devices through transactional publication, then start the scheduler. Make `StandardClosingBehavior` call the joining shutdown before `ContextManager.ReleaseContexts` or `OutputDeviceManagement.StopAll`; remove the existing `WindowsMultimedia` begin/end calls. Ensure application reload/stop paths that call `CloseExecution` inherit the same join guarantee.

Add tests proving export during playback does not let regular frame updates mutate state while compiling, the compiler context is the only context advanced during export, all recorded devices and contexts return to their prior running/paused behavior after successful and failing exports, and an already-closed engine remains closed after export. Add a concurrency test that attempts a normal lifecycle action while quiesced and proves it cannot alter export state.

Acceptance: exporting while a sequence is playing produces deterministic compiled output without concurrent live updates; after export (including a thrown export failure), the user sees the same device and context running/paused behavior as before, without a close/reopen cycle.

### Milestone 5 — Compatibility deprecation, timing evidence, and cleanup

On public `IOutputDevice`, mark `UpdateInterval` and `UpdateSignaler` with `[Obsolete]` messages stating that VIX-3750 uses `VixenSystem.DefaultUpdateInterval` and no longer supports device-owned scheduler signaling. Propagate matching obsolete annotations and XML documentation to public/protected implementations and related public contracts (`IOutputModule`, `IOutputModuleConsumer`, `IOutputter`, and `IOutputDeviceUpdateSignaler`) after locating all usages. Do not remove the members, serialization fields, or module implementations in this release. Remove their scheduling reads, legacy output-device timing instrumentation, and dead thread/update-signaler code only when confirmed unused outside the compatibility surface. Read the project `csharp-docs` skill before modifying these public interfaces and document the migration path in their XML comments.

Add a performance harness under `src/Vixen.Tests` or an existing developer benchmark location chosen after inspecting test conventions. It must not make CI depend on p99 wall-clock limits; instead it records repeatable distributions and fails only on functional invariants. Run it manually on supported Windows 10/11 hardware with preview-only, one-controller, multiple-controller, and deliberately slow-controller scenarios at 25 ms and 50 ms. Report median, p95, p99, maximum frame-start lateness, interval error, missed deadlines, final-spin duration, update duration, controller-barrier duration, preview coalescing, fallback usage, and process CPU use. Verify spin is never over 500 microseconds and scheduler waiting/spinning is approximately no more than 2% of one logical core at 25 ms and 1% at 50 ms excluding work. Treat p99 lateness of 1 ms or less as the target, not a hard real-time promise; if missed, record the distribution and smallest effective spin budget rather than adding MMCSS or raising priority.

Run `git diff --check` and correct trailing whitespace and missing final newlines in touched VIX-3750 files only. Do not reformat unrelated code.

Acceptance: builds show obsolete warnings only where expected, no code uses device cadence/signaling for scheduling, the instrumentation screen exposes the new values, and the performance report documents both timing configurations and whether the high-resolution or fallback timer was active.

### Milestone 6 — Skills, full validation, and manual behavior verification

Before finalizing, read and apply `.agents/skills/dotnet-best-practices/SKILL.md` to the complete C# diff. Read and apply `.agents/skills/csharp-async/SKILL.md` to scheduler cancellation, task barriers, UI dispatch, and lifecycle joins. If any public/protected API changed in Milestones 3 or 5, apply `.agents/skills/csharp-docs/SKILL.md`. Record every material finding, including consciously rejected guidance, in this plan's Decision Log. These are repository-required project skills, not optional generic reviews.

Run the focused scheduler/export tests, the full test project, and the Release/x64 solution rebuild. Then manually start the application and verify: a preview-only profile activates the loop; stopping all devices makes it idle; pause/resume of controller and preview stops/starts their updates; two active controllers receive concurrent output; an intentionally blocked preview does not hold output; export during playback does not visibly advance live output and restores prior behavior; and repeated opening/closing does not leave a second loop or native handles. Inspect instrumentation values and attach the performance-harness summary to the implementation handoff or issue.

Update `Progress`, `Surprises & Discoveries`, `Decision Log`, `Outcomes & Retrospective`, all acceptance evidence, and the change note at the bottom of this plan as work occurs.

## Concrete Steps

Run every command from `C:\Dev\Vixen` in PowerShell. Do not start a new Vixen application instance while a prior debug instance is holding build output DLLs; ask the user to close any active instance rather than terminating it.

1. Inspect current changes before beginning implementation:

       git status --short
       git diff --check

2. Implement and test Milestone 1 first:

       dotnet test src/Vixen.Tests/Vixen.Tests.csproj --filter "FullyQualifiedName~Execution"

   Expect all new deterministic scheduler tests to pass with `0 failed`. The exact test count grows as tests are added; record it in `Progress` rather than hard-coding a stale count.

3. After each following milestone, repeat focused tests for its namespace or class and then:

       dotnet test src/Vixen.Tests/Vixen.Tests.csproj

   Expect the complete suite to report `0 failed`.

4. Before manual validation and again before handoff, run:

       msbuild Vixen.sln -m -t:restore -t:Rebuild -p:Configuration=Release -p:Platform=x64
       git diff --check

   Expect the build to finish with `0 Error(s)` and no whitespace errors. Existing `NU1904` warnings for LiteDB 4.1.4 may still appear and are unrelated unless their status changes.

5. Run the Milestone 5 Windows performance harness at 25 ms and 50 ms under each listed workload. Save its summarized output in the implementation evidence and update the plan's outcome section; do not claim the timing target without a recorded measurement.

## Validation and Acceptance

The implementation is accepted only when all of the following observable behavior and automated proof exists:

- Paused controllers and previews receive zero frame updates until resumed. A controller cannot receive an update until `Start()` completed and cannot receive one once stop or pause begins.
- Exactly one scheduler instance exists. Closing cancels and joins it, including an in-flight controller barrier, before contexts and modules are released; close/open races do not update twice.
- Every active controller receives the same monotonically increasing frame ID. Tests prove all controller updates are launched before waiting, and no next frame begins until slow and fast consumers both finish the current frame.
- A throwing controller retries on later frames, healthy controllers keep receiving output, and exactly five consecutive failed frames remove/stop the failed controller. A slow controller is never overlapped, never arbitrarily cancelled, and records its delay/missed deadlines.
- Preview rendering is outside the controller barrier. One blocked or failing preview does not slow controller output; a blocked UI queue has at most one pending callback per preview and renders the latest frame after release.
- With no active controller or preview, the scheduler blocks without periodic polling and does not increment the refresh rate. The first newly active consumer gets an immediate frame with no idle-time missed-deadline count.
- Absolute deadline tests prove no catch-up burst and no permanent deadline drift after a late frame. Instrumentation exposes lateness and missed intervals, and the final spin never exceeds 500 microseconds.
- High-resolution timer and forced-fallback tests prove wait interruption, creation/disposal, and fallback logging. No MMCSS registration is present in this change.
- Export acquires exclusive quiescence, regular frames cannot alter its compilation state, and both success and failure restore prior device/context running/paused behavior without `CloseExecution()` disposing normal contexts.
- Per-device cadence/signal APIs are marked obsolete and not used by the scheduler. The full `Vixen.Tests` suite passes, Release/x64 build has no new errors, and `git diff --check` succeeds.

## Idempotence and Recovery

All code and test edits are additive or replace the current single scheduler path; rerunning focused/full tests and rebuilding is safe. Timer objects must support repeated construction/disposal and the fallback must pair every successful `timeBeginPeriod(1)` with exactly one `timeEndPeriod(1)`, including error and cancellation paths. A failed device start remains unpublished and can be retried by the normal Start action. A failure during export must restore state in `finally` and release the quiesce lease; if recovery itself fails, log each failed restoration and leave execution safely quiesced/closed rather than starting an uncontrolled loop.

Avoid destructive source-control commands. If performance testing needs a real controller, use a test profile or disconnected/safe output configuration. Never use a live lighting setup as an uncontrolled timing test.

## Artifacts and Notes

The source review is `docs/reviews/vix-3750-core-engine-timing-review.md`. Its verified baseline was a successful `Vixen_Tests` Release/x64 build and `dotnet test --no-build --no-restore` with 490 passing tests, with no reviewed test changes. Re-establish and record the current baseline before implementation because test totals may have changed.

The relevant implementation locations are:

- `src/Vixen.Core/Sys/Execution.cs` — public/static facade to be reduced to lifecycle and scheduler delegation.
- `src/Vixen.Core/Sys/Execution/` — new internal scheduler, timer, mailbox, frame contracts, and metrics implementation.
- `src/Vixen.Core/Sys/Managers/OutputDeviceExecution.cs` and `IOutputDeviceExecution.cs` — active snapshot publication and lifecycle barriers.
- `src/Vixen.Core/Sys/Output/BasicOutputModuleExecutionControl.cs`, `OutputController.cs`, and `OutputPreview.cs` — transactional lifecycle and controller/preview adaptation.
- `src/Vixen.Core/Module/Preview/PreviewModuleInstanceBase.cs` — preview dispatch boundary.
- `src/Vixen.Core/Cache/Sequence/SequenceIntervalGenerator.cs` — export quiesce and restoration.
- `src/Vixen.Core/Sys/State/Execution/Behavior/StandardOpeningBehavior.cs`, `StandardClosingBehavior.cs`, and `WindowsMultimedia.cs` — correct shutdown ordering and removal of incorrect multimedia use.
- `src/Vixen.Core/Sys/Instrumentation/` — new visible metrics.
- `src/Vixen.Tests/Sys/Execution/` and the selected performance-harness location — deterministic and measured validation.

## Interfaces and Dependencies

All new scheduler infrastructure remains internal to `Vixen.Core`; do not add NuGet packages. Use the .NET base class library (`Stopwatch`, `CancellationToken`, `Task`, `TaskCompletionSource`, `SafeWaitHandle`, `WaitHandle`, and `Marshal`) and direct Windows P/Invoke only in `WindowsHighResolutionExecutionTimer`.

Define the equivalent of the following internal contracts; names may vary only when a better repository-consistent name is recorded in the Decision Log:

    internal interface IExecutionTimer : IDisposable
    {
        long Timestamp { get; }
        long Frequency { get; }
        ExecutionTimerWaitResult WaitUntil(long deadlineTimestamp, CancellationToken cancellationToken);
        void Wake();
    }

    internal interface IPreviewFramePublisher
    {
        void Publish(PreviewFrameSnapshot snapshot);
        void Invalidate();
    }

`ControllerUpdateResult` must include controller identity, frame ID, completion/failure status, exception details when present, duration, and consecutive-failure outcome. `PreviewFrameSnapshot` must include its frame ID and specify its immutable/double-buffer ownership; it must not allow a preview to retain engine-mutable data after callback completion. `ExecutionFrameMetrics` records frame scheduling and the values named in Milestone 1.

`Execution` must expose internal lifecycle operations equivalent to:

    internal static void Startup();
    internal static void Shutdown(); // cancellation plus join before return
    internal static ExecutionQuiesceLease Quiesce();

The quiesce lease is `IDisposable` or `IAsyncDisposable` according to the chosen no-deadlock calling path, supports nesting, and is released from export's `finally`. Its public accessibility must not exceed `internal` unless a separately documented product requirement requires it.

On the existing public `Vixen.Sys.Output.IOutputDevice`, retain but obsolete:

    [Obsolete("Per-device scheduling is no longer supported. The execution scheduler uses VixenSystem.DefaultUpdateInterval.")]
    int UpdateInterval { get; set; }

    [Obsolete("Per-device update signaling is no longer supported. The execution scheduler controls frame dispatch.")]
    IOutputDeviceUpdateSignaler UpdateSignaler { get; }

No implementation in this plan may use either member to decide whether or when to render/send a frame. `IOutputDevice.UpdateAsync()` is removed because it was an unused POC-only API; synchronous controller output is dispatched concurrently by the execution loop.

Plan change note (2026-08-25): Initial ExecPlan created from the VIX-3750 timing review and requester decisions. It records the explicit deprecation, retry, export-restoration, and MMCSS-scope choices so implementation does not infer them later.

Plan change note (2026-08-25): Marked Milestone 1 complete after adding the scheduler/timer/metrics foundation and deterministic tests. Documented the necessary `Vixen.Sys.Engine` namespace choice after the existing `Vixen.Sys.Execution` type prevented the originally suggested namespace.

Plan change note (2026-08-25): Marked Milestone 2 complete after integrating leased active snapshots, transactional lifecycle publication, scheduler wake notifications, concurrent controller dispatch, the per-frame barrier, and bounded consecutive-failure handling. The Rider-visible console-window test was removed before validation because it exercised native timer construction; Windows integration coverage remains deferred to the later performance milestone.

Plan change note (2026-08-25): Removed the unused POC-only `IOutputDevice.UpdateAsync()` API and its built-in implementations after the requester confirmed there are no external consumers. Simplified controller dispatch to direct scheduler-owned tasks while retaining the common barrier and per-controller failure isolation.

Plan change note (2026-08-25): Marked Milestone 3 complete after the requester confirmed the full test suite passed. Marked Milestone 4 complete after replacing export close/reopen with nested scheduler quiescence, capturing/restoring output and context behavior, serializing ordinary output lifecycle actions behind the export lease, and removing obsolete Windows multimedia resolution calls. Focused scheduler tests passed 6/6 after a Visual Studio MSBuild build; the normal full CLI run stalled in its existing test host and was stopped.

Plan change note (2026-08-25): Corrected the Milestone 4 lifecycle-gate test so it releases the quiesce lease before awaiting the intentionally blocked lifecycle operation. This preserves the intended assertion and prevents the full test suite from deadlocking.

Plan change note (2026-08-25): Made the Milestone 4 in-flight-frame quiesce test deterministic by exposing internal scheduler quiescence state for test synchronization, replacing a scheduling-dependent yield, and always releasing the blocked frame during cleanup.

Plan change note (2026-08-25): Implemented the code portion of Milestone 5: obsolete compatibility annotations and migration XML documentation, scheduler instrumentation registration and units, active/failure/preview/fallback metrics, and removal of unused legacy device-timing values. Real Windows performance measurements remain pending because the required controlled output scenarios are not available in this workspace.

Plan change note (2026-08-25): Recorded the requester's preview-only timing baseline. It confirms preview coalescing is not the source of the observed timing variation, but does not yet identify the scheduler interval or high-resolution/fallback timer mode required to interpret the wake lateness.

Plan change note (2026-08-25): Updated the preview-only baseline with its 50 ms global interval. The observed maximum lateness consumes 30% of that frame budget; timer mode remains needed before changing scheduler behavior.

Plan change note (2026-08-25): Recorded that the high-resolution timer was active for the preview-only baseline. This rules out the fallback path; frame-update duration is the next required measurement before attributing the lateness to timer wake behavior.

Plan change note (2026-08-25): Recorded a 10–20 ms frame-update duration for the high-resolution, preview-only baseline. Together with the 15–30 ms sleep range, normal work and waiting consume up to the entire 50 ms cadence; do not add MMCSS or priority changes without the remaining scenario evidence.

Plan change note (2026-08-25): Attributed approximately 14 ms of the preview-only frame-update duration to `ContextManager.Update()`. This narrows future optimization investigation to context execution rather than preview dispatch, controller output, or scheduler priority.
