# Architecture & Design Decisions

Why the codebase is shaped the way it is, consolidated into one place. Most
of these decisions already exist as comments at their point of use; this
document pulls them together with the reasoning that connects them, in
roughly the order they were made. See the README's "Some findings" section
for the underlying hardware/runtime discoveries some of these decisions are
built on (DTR gating, `SerialStream` cancellation).

## D1 — Layered library split, one-way dependency

`MedDeviceSim` (UI) → `MedDeviceSim.Session` → `MedDeviceSim.Workflow` →
`MedDeviceSim.Communication`. Nothing lower in that chain knows anything
about a layer above it — `TreatmentWorkflow` has zero knowledge a UI, or
even a real transport, exists.

**Why:** each layer can be tested and reasoned about in isolation.
`TreatmentWorkflow`'s entire test suite runs with no I/O and no hardware.
**When:** introduced in the Phase 2 refactor, only after Phase 1's console
experiments had already proven the underlying serial round-trip worked —
the abstraction was extracted from working code, not designed up front.

## D2 — `TreatmentWorkflow` is pure and synchronous

No `async`, no I/O, no dependency on `ITransport`. It only answers "is this
action valid right now, and if so what command does it produce" and "given
this response, what state comes next."

**Why:** testable without hardware, without a UI, and without async test
infrastructure — see `TreatmentWorkflowTests`, which is entirely synchronous
`[Fact]`s. It also means `TreatmentWorkflow` needs no thread-safety of its
own, since single-threaded, synchronous access is guaranteed by construction
— relevant later (see D6).

## D3 — Request validation and response application are separate steps

`RequestArm()` (etc.) only checks whether the action is currently valid and
returns a `WorkflowResult` — `Accepted` with the `DeviceCommand` to send, or
`Rejected` with a reason. `CurrentState` doesn't change here. It only changes
later, in `OnResponse`, once a response confirms the device actually did it.

**Why:** this is what lets `TreatmentSession.ExecuteAsync` reject an invalid
action locally, before any transport I/O happens (REQ-016) — an invalid
`ARM` never touches the wire. It also means state reflects confirmed device
behavior, not merely attempted requests: if the device never replies, the
state machine correctly stays wherever it was, not wherever the request
hoped to end up.

## D4 — `WorkflowResult` / `SessionResult` never throw for expected outcomes

An invalid action, a communication failure, and a malformed response are all
ordinary return values (`Rejected`, `CommunicationFailed`, `Sent(Unknown(...))`),
not exceptions.

**Why:** a user clicking a button that isn't currently valid, or a device
sending garbage, is a normal, expected occurrence in this domain — not
exceptional control flow. Exceptions are reserved for things that are
genuinely unexpected (see D7).

## D5 — `ITransport` was extracted only when a second implementation needed it

`SerialTransport` existed alone through Phase 2 with no interface. `ITransport`
was extracted in Phase 3, at the exact point `TreatmentSession`'s tests
needed a fake to substitute for real hardware — not speculatively introduced
earlier "in case it's needed."

**Why:** avoids designing an abstraction around guesses about a future
second implementation. By the time `ITransport` existed, its shape was
already validated by `SerialTransport`'s real, working usage — and later,
`TcpTransport` (Phase 6) confirmed the interface generalized correctly to a
completely different underlying transport (sockets vs. `SerialPort`) without
needing to change.

## D6 — `TreatmentSession` is caller-driven, not a background read loop

There is no thread continuously reading the transport in the background.
Every `TreatmentSession` method — including `ReadNextUpdateAsync`, added in
Phase 6 for live `PROGRESS` updates — only reads when a caller calls it, and
`CurrentState` only ever changes on whatever thread the caller is running on.

**Why:** a background loop would require making `TreatmentWorkflow`
thread-safe (D2's simplicity would be lost) to guard against the UI thread
and a background reader touching `CurrentState` concurrently. Nothing has
justified that cost yet — the UI instead runs its own foreground loop
(`Form1.ObserveUpdatesAsync`) that calls `ReadNextUpdateAsync` repeatedly
while `Running`, achieving the same live-update effect without a second
thread touching workflow state.

## D7 — Synchronous `Read`/`Write` wrapped in `Task.Run`, not native async I/O

Both `SerialTransport.SendAsync`/`LineReader.ReadLineAsync` and (for reads)
`TcpTransport` fall back to `Stream.Read`/`Write` via `Task.Run` rather than
calling `ReadAsync`/`WriteAsync` directly for the read path.

**Why:** confirmed via an isolated diagnostic probe against real hardware in
Phase 1 that `SerialStream`'s async `ReadAsync` does not reliably honor its
`CancellationToken` on Windows (a call hung 45+ seconds past a 2-second
token) — but the synchronous `Read` reliably respects `ReadTimeout`. Wrapping
the synchronous call in `Task.Run` and polling between chunks was the
workaround that Phase 1's console experiments validated first. `TcpTransport`
inherited this for reads (via reusing `LineReader` — see D8) but uses
`NetworkStream.WriteAsync` directly for writes, since `NetworkStream`'s async
methods are well-established .NET and not known to share the same gap — an
explicit, acknowledged difference in confidence level (verified for
`SerialStream`, assumed by track record for `NetworkStream`), not an
oversight.

## D8 — `LineReader` is transport-agnostic, with one deliberate exception

`LineReader` only depends on `Stream`, so `TcpTransport` (Phase 6) reuses it
unmodified — exactly the payoff D5's "extract when a second consumer needs
it" reasoning predicts. The one place `LineReader` isn't fully agnostic:
its retry-on-timeout loop has to recognize *two* different exception shapes
for "no data within `ReadTimeout`" — `SerialStream`'s `TimeoutException` and
`NetworkStream`'s `IOException` wrapping a `SocketException`.

**Why not push this into each transport instead?** The retry logic itself
(loop back and check cancellation again) is identical either way; only the
exception shape differs. Handling both shapes in one place was judged a
smaller, more honest compromise than duplicating the retry loop per
transport. Verified necessary, not assumed: an early `TcpTransport` version
without the second `catch` hung indefinitely once no more data was coming.

## D9 — `SimulatedDeviceServer` is an independent protocol implementation, not a call into `TreatmentWorkflow`

`SimulatedDeviceServer.HandleLine` re-implements the same state rules
(`ARM` requires a loaded plan, etc.) from scratch, rather than sharing
`TreatmentWorkflow`'s switch expression.

**Why:** if both sides of the wire ran the exact same code, agreement
between them would be guaranteed by construction and would prove very
little. Two separately-reasoned-about implementations of the same protocol
agreeing (`TreatmentSessionAgainstSimulatorTests`) is a meaningfully
stronger correctness signal than one implementation tested against itself.

## D10 — The simulator ships as a standalone host process, not just test infrastructure

`MedDeviceSim.Simulator.Host` is a separate console app (`dotnet run
--project MedDeviceSim.Simulator.Host`) wrapping `SimulatedDeviceServer`,
distinct from `MedDeviceSim.Simulator.Tests`.

**Why:** lets the real WinForms UI run a full `Connected → Complete` workflow
against something live over TCP, for manual testing and demonstration —
mirroring how a real device is a separate process the host application
talks to, not something that only exists inside a test run.

## D11 — `FakeTransport` and `SimulatedDeviceServer` coexist; neither replaced the other

`FakeTransport` (Phase 5, `MedDeviceSim.Session.Tests`) is a scripted test
double — enqueue exact lines, throw a specific exception on the next read.
`SimulatedDeviceServer` (Phase 6) is a real, stateful device implementation
reachable over TCP. Building the latter did not remove the former.

**Why:** `FakeTransport` gives precise, cheap control over exact byte
sequences, timing, and failure injection (malformed lines, mid-read
disconnects, arbitrary exceptions) that would be awkward or slow to
reproduce through a real socket. `SimulatedDeviceServer` proves the protocol
is actually implemented correctly end-to-end. They test different things and
both remain valuable.

## D12 — The UI reflects workflow state; it does not enforce it

`Form1.UpdateButtonStates` disables buttons that the current state wouldn't
allow, but this is UX polish only — `TreatmentWorkflow` rejects an invalid
action regardless of what the UI permitted (REQ-052). The UI could be
deleted entirely and every safety property would still hold.

**Why:** keeps the single source of truth for validity in one place (D1/D2),
consistent with the whole layering strategy — a UI bug (a button wrongly
enabled) can produce a rejected action and a log line, never an invalid
state transition.
