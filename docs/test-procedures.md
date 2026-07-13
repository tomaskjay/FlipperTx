# Test Procedures

How this project is actually tested, at every level from pure unit tests up
to manual verification against real hardware. See
[`docs/requirements.md`](requirements.md) for what's being verified and
`traceability-matrix.md` (once written) for which test verifies which
requirement.

## Levels

| Level | Where | Requires hardware/network? |
|---|---|---|
| Pure unit | `MedDeviceSim.Workflow.Tests`, `MedDeviceSim.Communication.Tests` (protocol parsing/formatting, `LineReader`) | No |
| Integration via test double | `MedDeviceSim.Session.Tests` (`FakeTransport`) | No |
| Integration via real socket | `MedDeviceSim.Communication.Tests` (loopback `LineReader` timeout test), `MedDeviceSim.Simulator.Tests` (`TreatmentSessionAgainstSimulatorTests`) | No (loopback TCP only) |
| Hardware-gated | `MedDeviceSim.Communication.Tests` (`SerialTransportHardwareTests`) | Yes — real Flipper Zero over USB |
| Manual UI | `MedDeviceSim` run directly | Optional — TCP simulator or real hardware |

## Running the automated suite

```
dotnet test
```

from the repo root runs every automated test project. As of this writing:
68 passed, 2 skipped (hardware-gated, see below), 0 failed, across
`MedDeviceSim.Workflow.Tests`, `MedDeviceSim.Communication.Tests`,
`MedDeviceSim.Session.Tests`, and `MedDeviceSim.Simulator.Tests`. None of
this requires a Flipper, a network connection, or any manual setup — this is
what CI (if this project had any) would run.

To run one project in isolation: `dotnet test <ProjectName>`, e.g.
`dotnet test MedDeviceSim.Simulator.Tests`.

### What each project covers

- **`MedDeviceSim.Workflow.Tests`** — every `TreatmentWorkflow` request/response
  transition in isolation (REQ-011–REQ-021), including rejected actions and
  ignored unexpected responses. No I/O anywhere in this project.
- **`MedDeviceSim.Communication.Tests`** — `DeviceCommand`/`DeviceResponse`
  wire formatting and parsing (REQ-030–REQ-033), `LineReader`'s framing
  (split/combined lines, REQ-035) and its real-socket timeout-retry behavior,
  plus the hardware-gated `SerialTransportHardwareTests`.
- **`MedDeviceSim.Session.Tests`** — `TreatmentSession` against `FakeTransport`:
  the happy path, a rejected action never reaching the transport, and
  `TreatmentSessionFaultTests` covering timeouts, disconnects, cancellation,
  malformed responses, and device errors (REQ-040–REQ-044) with exact,
  scripted control over what the fake transport does.
- **`MedDeviceSim.Simulator.Tests`** — `SimulatedDeviceServerTests` (the
  simulator's own protocol correctness in isolation) and
  `TreatmentSessionAgainstSimulatorTests` (the full host-side `TreatmentSession`
  driven against the simulator over a real TCP socket — the flagship
  Connected→Complete and STOP/unsolicited-PROGRESS-race regression tests).

## Hardware-gated tests

`SerialTransportHardwareTests` requires a real Flipper Zero connected over
USB and is **not** part of the default `dotnet test` run — both `[Fact]`s
carry `[Fact(Skip = "requires a real Flipper Zero connected via USB")]`.

To run them:

1. Connect the Flipper Zero over USB and note its COM port (Device Manager).
2. In `MedDeviceSim.Communication.Tests/SerialTransportHardwareTests.cs`,
   temporarily remove both `Skip = "..."` arguments, and update the
   hardcoded `"COM7"` port name if yours differs.
3. `dotnet test MedDeviceSim.Communication.Tests`
4. **Revert step 2 before committing** — these tests must stay skipped in
   the default run, since CI-equivalent runs and anyone without a Flipper
   attached would otherwise fail on them.

These confirm the transport layer against real hardware directly: that a
welcome banner arrives, and that sending one of this project's custom
commands to the stock (protocol-unaware) Flipper CLI parses safely as
`Unknown` rather than crashing anything (REQ-033).

## Manual UI verification

There's no automated UI test suite (WinForms UI testing wasn't built out
this project) — the UI is verified manually, in two ways:

### Against the TCP simulator (reaches full Connected → Complete)

1. `dotnet run --project MedDeviceSim.Simulator.Host` — starts the simulator
   listening on `127.0.0.1:9000`.
2. `dotnet run --project MedDeviceSim` — starts the UI.
3. Select the TCP option, enter port `9000`, click Connect.
4. Drive `LOAD_PLAN` → `ARM` → `START`, and confirm:
   - the transport-open and workflow-state labels update correctly and
     independently (REQ-051),
   - each action button is enabled only when the current state allows it
     (REQ-052),
   - the progress bar appears and updates live while Running, with no
     further clicks (REQ-054),
   - the event log records every sent action and received response,
     timestamped (REQ-053).
5. Let a run reach `COMPLETE`, or click Stop mid-run and confirm `STOPPED`
   is reached even with a `PROGRESS` line already queued (the regression
   this checkpoint's `TreatmentSessionAgainstSimulatorTests` case covers).
6. Close the window while connected (including mid-run) and confirm no
   crash/hang — this is checking REQ-055's resource cleanup
   (`Form1_FormClosing` disposing the session and cancelling the update loop).

### Against real hardware (stock Flipper CLI — stops at `Connected`)

Same steps via the Serial option and a COM port instead of TCP. Since the
stock Flipper CLI doesn't understand this protocol, `CONNECT` will get an
unrecognized-command reply, not `CONNECTED` — the workflow correctly stays
at `Disconnected` while the transport itself is confirmed open and
round-tripping real bytes. This path exists to verify the serial transport
layer against real hardware, not to reach further workflow states — that's
what the TCP simulator path is for.
