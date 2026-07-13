# Requirements-to-Tests Traceability Matrix

Maps every requirement in [`requirements.md`](requirements.md) to the
test(s) that verify it. "Requirement" here is paraphrased for scanability —
see `requirements.md` for the authoritative wording. Test names are
qualified as `Project.Class.Method`; see
[`test-procedures.md`](test-procedures.md) for how to run them and what
"Automated" vs. "Hardware-gated" vs. "Manual only" mean.

Built by tracing each requirement forward to real test code, not by
recalling what should exist — one requirement (REQ-018) turned out to be
mis-stated once actually checked against test behavior; see the
[Notes](#notes) at the bottom.

## Connection & Transport

| ID | Requirement | Verified by | Status |
|---|---|---|---|
| REQ-001 | Serial transport support | `Communication.Tests.SerialTransportHardwareTests.OpenSendReadLine_AgainstRealFlipper_ReceivesWelcomeBanner` | Hardware-gated (skipped by default) |
| REQ-002 | TCP transport support | `Simulator.Tests.SimulatedDeviceServerTests.FullHappyPath_RawProtocol_ConnectThroughStop`, `Simulator.Tests.TreatmentSessionAgainstSimulatorTests.FullHappyPath_ConnectThroughRunning_AgainstRealSimulator` | Automated |
| REQ-003 | `ITransport` is transport-agnostic | `Session.Tests.TreatmentSessionTests.*` (via `FakeTransport`) and `Simulator.Tests.TreatmentSessionAgainstSimulatorTests.*` (via `TcpTransport`) — same `TreatmentSession` code path, two different transports | Automated |
| REQ-004 | `OpenAsync` distinct from `ConnectAsync` | `Simulator.Tests.TreatmentSessionAgainstSimulatorTests.FullHappyPath_ConnectThroughRunning_AgainstRealSimulator` (calls both, separately) | Automated (the "transport open, protocol never reaches Connected" case is manual-only — see REQ-004 gap below) |
| REQ-005 | Communication failure treated as lost connection | `Session.Tests.TreatmentSessionFaultTests.ArmAsync_WhenTransportTimesOut_ReturnsCommunicationFailedAndForcesDisconnected`, `.ArmAsync_WhenUnexpectedDisconnectOccurs_ReturnsCommunicationFailedAndForcesDisconnected` | Automated |

## Workflow / State Machine

| ID | Requirement | Verified by | Status |
|---|---|---|---|
| REQ-010 | All eight states reachable | `Workflow.Tests.TreatmentWorkflowTests.InitialState_IsDisconnected`, `.FullHappyPath_ConnectThroughComplete_EndsInCompleteWithPlanId`, `.OnResponse_Stopped_WhileArmed_TransitionsToStoppedWithPlanId`, `.OnResponse_Error_WhileConnected_TransitionsToFault` | Automated |
| REQ-011 | `CONNECT` only from Disconnected | `Workflow.Tests.TreatmentWorkflowTests.RequestConnect_WhileDisconnected_IsAcceptedWithConnectCommand`, `.RequestConnect_WhileAlreadyConnected_IsRejected` | Automated |
| REQ-012 | `LOAD_PLAN` only from Connected | `.RequestLoadPlan_WhileConnected_IsAcceptedWithPlanId`, `.RequestLoadPlan_WhileDisconnected_IsRejected`, `.RequestLoadPlan_WhileRunning_IsRejected` | Automated |
| REQ-013 | `ARM` only from PlanLoaded | `.RequestArm_WithoutPlanLoaded_IsRejected`, `.RequestArm_WithPlanLoaded_IsAccepted` | Automated |
| REQ-014 | `START` only from Armed | `.RequestStart_WithoutArming_IsRejected`, `.FullHappyPath_ConnectThroughComplete_EndsInCompleteWithPlanId` (positive case) | Automated |
| REQ-015 | `STOP` only from Armed or Running | `.RequestStop_WhileArmed_IsAccepted`, `.RequestStop_WhileRunning_IsAccepted`, `.RequestStop_WhileConnected_IsRejected` | Automated |
| REQ-016 | Rejected action never reaches the transport | `Session.Tests.TreatmentSessionTests.ArmAsync_WithoutPlanLoaded_IsRejectedAndSendsNothing` | Automated |
| REQ-017 | Lost connection forces Disconnected from anywhere | `Workflow.Tests.TreatmentWorkflowTests.OnDisconnected_WhileRunning_ForcesDisconnected`, `.OnDisconnected_WhileAlreadyDisconnected_StaysDisconnected`, `Session.Tests.TreatmentSessionFaultTests.ArmAsync_WhenTransportTimesOut_...`, `.ArmAsync_WhenUnexpectedDisconnectOccurs_...` | Automated |
| REQ-018 | Device `ERROR` forces Fault (comm failure does not) | `Workflow.Tests.TreatmentWorkflowTests.OnResponse_Error_WhileRunning_TransitionsToFaultWithReason`, `.OnResponse_Error_WhileConnected_TransitionsToFault`, `Session.Tests.TreatmentSessionFaultTests.ArmAsync_WhenDeviceReportsError_TransitionsToFault` | Automated |
| REQ-019 | Unexpected response ignored | `Workflow.Tests.TreatmentWorkflowTests.OnResponse_UnexpectedForCurrentState_IsIgnored` | Automated |
| REQ-020 | Plan ID threads through every subsequent state | `.OnResponse_PlanLoaded_WhileConnected_TransitionsToPlanLoadedWithId`, `.OnResponse_Ready_WhilePlanLoaded_TransitionsToArmedWithSamePlanId`, `.FullHappyPath_ConnectThroughComplete_EndsInCompleteWithPlanId`, `.OnResponse_Stopped_WhileArmed_TransitionsToStoppedWithPlanId` | Automated |
| REQ-021 | `PROGRESS` updates percent without changing state | `Workflow.Tests.TreatmentWorkflowTests.FullHappyPath_ConnectThroughComplete_EndsInCompleteWithPlanId`, `Simulator.Tests.TreatmentSessionAgainstSimulatorTests.FullHappyPath_ConnectThroughComplete_AgainstRealSimulator` | Automated |

## Protocol

| ID | Requirement | Verified by | Status |
|---|---|---|---|
| REQ-030 | Line-based, `\r\n`-terminated | `Communication.Tests.DeviceCommandTests.*` (all six `ToWireFormat_*`), `.LineReaderTests.ReadLineAsync_SingleCompleteLine_ReturnsLineWithoutTerminator` | Automated |
| REQ-031 | Exactly six outgoing commands | `Communication.Tests.DeviceCommandTests.ToWireFormat_Connect_ReturnsConnect`, `.ToWireFormat_LoadPlan_IncludesPlanId`, `.ToWireFormat_Arm_ReturnsArm`, `.ToWireFormat_Start_ReturnsStart`, `.ToWireFormat_Stop_ReturnsStop`, `.ToWireFormat_GetStatus_ReturnsGetStatus` | Automated |
| REQ-032 | Exactly eight recognized responses | `Communication.Tests.DeviceResponseTests.Parse_Connected_ReturnsConnected`, `.Parse_PlanLoaded_ReturnsPlanLoadedWithId`, `.Parse_Ready_ReturnsReady`, `.Parse_Running_ReturnsRunning`, `.Parse_Progress_ReturnsProgressWithPercent`, `.Parse_Complete_ReturnsComplete`, `.Parse_Stopped_ReturnsStopped`, `.Parse_Error_ReturnsErrorWithReason` | Automated |
| REQ-033 | Unrecognized input parses to Unknown, never throws | `Communication.Tests.DeviceResponseTests.Parse_MalformedOrUnrecognizedInput_ReturnsUnknown` (8 cases), `.SerialTransportHardwareTests.SendArm_AgainstRealFlipperStockCli_AllOutputParsesAsUnknown` | Automated (+ hardware-gated real-world case) |
| REQ-034 | Only `PROGRESS`/`COMPLETE` may arrive unprompted | `Simulator.Tests.SimulatedDeviceServerTests.Start_WithoutStopping_AutonomouslyEmitsProgressThenComplete`, `.TreatmentSessionAgainstSimulatorTests.StopAsync_WhileUnreadProgressIsQueued_StillReachesStopped` | Automated |
| REQ-035 | Split/combined lines reassembled correctly | `Communication.Tests.LineReaderTests.ReadLineAsync_MultipleLinesInOneRead_ReturnsEachLineSeparately`, `.ReadLineAsync_LineSplitAcrossManyReads_StillAssemblesCorrectly` | Automated |

## Fault Handling

| ID | Requirement | Verified by | Status |
|---|---|---|---|
| REQ-040 | Read/write timeout → communication failure | `Session.Tests.TreatmentSessionFaultTests.ArmAsync_WhenTransportTimesOut_ReturnsCommunicationFailedAndForcesDisconnected`, `Communication.Tests.LineReaderTests.ReadLineAsync_NoDataWithinReadTimeout_RetriesUntilLineArrives` | Automated |
| REQ-041 | Unexpected disconnect → communication failure | `Session.Tests.TreatmentSessionFaultTests.ArmAsync_WhenUnexpectedDisconnectOccurs_ReturnsCommunicationFailedAndForcesDisconnected` | Automated |
| REQ-042 | Caller cancellation distinguished from failure | `Session.Tests.TreatmentSessionFaultTests.ArmAsync_WhenCallerCancels_PropagatesOperationCanceledExceptionInstead` | Automated |
| REQ-043 | Unsolicited `PROGRESS`/`COMPLETE` skipped, not mistaken for a reply | `Simulator.Tests.TreatmentSessionAgainstSimulatorTests.StopAsync_WhileUnreadProgressIsQueued_StillReachesStopped` | Automated |
| REQ-044 | Malformed response → Unknown, no state corruption | `Session.Tests.TreatmentSessionFaultTests.ArmAsync_WhenResponseIsMalformed_ReturnsSentWithUnknownAndLeavesStateUnchanged` | Automated |

## UI

| ID | Requirement | Verified by | Status |
|---|---|---|---|
| REQ-050 | Serial/TCP choice before connecting | Manual procedure, `test-procedures.md` § Manual UI verification | **Manual only — no automated UI test suite exists** |
| REQ-051 | Transport-open status separate from workflow state | Manual procedure | **Manual only** |
| REQ-052 | Button enablement mirrors workflow state | Manual procedure (the underlying enforcement itself — `TreatmentWorkflow` rejecting invalid actions regardless of UI state — is covered by REQ-011–REQ-015's automated tests; the UI wiring in `Form1.cs` is not) | **Manual only** |
| REQ-053 | Timestamped event log | Manual procedure | **Manual only** |
| REQ-054 | Live progress display while Running | Manual procedure | **Manual only** |
| REQ-055 | Resource cleanup on disconnect/close | Manual procedure (previously verified via Windows UI Automation, per the README) | **Manual only** |

## Coverage gaps

- **The entire UI category (REQ-050–REQ-055) has no automated test coverage.**
  Every one of these is verified only by the manual procedure in
  `test-procedures.md`. This is the single largest gap in this matrix — a
  regression in `Form1.cs` would not be caught by `dotnet test`.
- **REQ-001 and part of REQ-033** depend on hardware-gated tests that are
  skipped by default and only run manually with a Flipper attached — a
  regression here would not be caught by CI-equivalent runs either.
- **REQ-004's negative case** (transport open, protocol workflow never
  reaches Connected — the actual real-hardware situation) has no automated
  test; only the positive case (both succeed) is automated.

## Notes

While building this matrix, **REQ-018 as originally written was wrong**: it
claimed a communication failure (not just a device `ERROR` response) could
also force `Fault`. Tracing it to `TreatmentSessionFaultTests` showed
`ArmAsync_WhenTransportTimesOut_...` and
`ArmAsync_WhenUnexpectedDisconnectOccurs_...` both assert `Disconnected`, not
`Fault` — only `ArmAsync_WhenDeviceReportsError_TransitionsToFault` reaches
`Fault`. `requirements.md`, `state-machine.md`, and the README were all
corrected once the test evidence contradicted them. Left here as a record of
why this matrix is worth having: it caught a real, previously-undetected
documentation error by forcing every claim to point at actual test code.
