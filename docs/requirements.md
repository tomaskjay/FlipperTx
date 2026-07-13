# Requirements

Numbered requirements for the Medical Device Control Simulator, extracted from
behavior that is already implemented and enforced/tested in the codebase —
not aspirational. Each requirement will be linked to the test(s) that verify
it in `traceability-matrix.md`.

Numbering is a single flat sequence (`REQ-NNN`); the **Category** column
groups related requirements without reserving numeric ranges, so new
requirements can be appended without renumbering.

## Connection & Transport

| ID | Category | Requirement |
|---|---|---|
| REQ-001 | Connection & Transport | The system shall support communicating with a device over a serial (COM port) transport. |
| REQ-002 | Connection & Transport | The system shall support communicating with a device over a TCP transport. |
| REQ-003 | Connection & Transport | Both transport implementations shall conform to a single `ITransport` interface, so the rest of the system is transport-agnostic. |
| REQ-004 | Connection & Transport | Opening a transport shall be a distinct step from establishing the protocol-level connection (`CONNECT`/`CONNECTED`) — a transport can be open while the device does not speak the application protocol. |
| REQ-005 | Connection & Transport | A communication failure (I/O error, timeout, or unexpected disconnect) during any command exchange shall be treated as a lost connection, not surfaced as an undefined error. |

## Workflow / State Machine

| ID | Category | Requirement |
|---|---|---|
| REQ-010 | Workflow | The system shall enforce treatment operation as a state machine with the states: Disconnected, Connected, PlanLoaded, Armed, Running, Complete, Stopped, Fault. |
| REQ-011 | Workflow | `CONNECT` shall only be accepted while Disconnected. |
| REQ-012 | Workflow | `LOAD_PLAN` shall only be accepted while Connected. |
| REQ-013 | Workflow | `ARM` shall only be accepted once a plan has been loaded (PlanLoaded state). |
| REQ-014 | Workflow | `START` shall only be accepted while Armed. |
| REQ-015 | Workflow | `STOP` shall only be accepted while Armed or Running. |
| REQ-016 | Workflow | A rejected action shall not be sent to the device — rejection is decided locally before any transport I/O occurs. |
| REQ-017 | Workflow | A lost connection (transport failure or explicit disconnect) shall force the state machine to Disconnected immediately, regardless of the state it was in. |
| REQ-018 | Workflow | A device-reported error (`ERROR` response) received in any state other than Disconnected shall transition the state machine to a distinct Fault state, preserving the reason — distinct from a communication failure (REQ-017), which forces Disconnected instead, since losing the ability to talk to the device is not the same thing as the device reporting it is in a bad state. |
| REQ-019 | Workflow | A device response that does not match a valid transition for the current state shall be ignored, leaving the current state unchanged. |
| REQ-020 | Workflow | The plan ID established at `LOAD_PLAN` shall be preserved through every subsequent state (PlanLoaded, Armed, Running, Complete, Stopped) until the next disconnect. |
| REQ-021 | Workflow | `PROGRESS` updates received while Running shall update the current percent-complete without changing state. |

## Protocol

| ID | Category | Requirement |
|---|---|---|
| REQ-030 | Protocol | Commands and responses shall be line-based, terminated with `\r\n`. |
| REQ-031 | Protocol | The system shall define exactly six outgoing commands: `CONNECT`, `LOAD_PLAN <id>`, `ARM`, `START`, `STOP`, `GET_STATUS`. |
| REQ-032 | Protocol | The system shall recognize exactly eight response shapes: `CONNECTED`, `PLAN_LOADED <id>`, `READY`, `RUNNING`, `PROGRESS <percent>`, `COMPLETE`, `STOPPED`, `ERROR <reason>`. |
| REQ-033 | Protocol | A line that does not match any recognized response shape shall parse to a distinct Unknown response rather than throwing, so unexpected device output is a handleable case, not an exceptional one. |
| REQ-034 | Protocol | `PROGRESS` and `COMPLETE` are the only response kinds that may arrive unprompted (not as a direct reply to the most recently sent command); every other response kind is guaranteed to be a direct reply. |
| REQ-035 | Protocol | A single line split across multiple reads shall be reassembled correctly, and multiple lines delivered in one read shall be split correctly. |

## Fault Handling

| ID | Category | Requirement |
|---|---|---|
| REQ-040 | Fault Handling | A read/write timeout on the transport shall be treated as a communication failure, not an unhandled exception. |
| REQ-041 | Fault Handling | An unexpected mid-operation disconnect shall be treated as a communication failure, not an unhandled exception. |
| REQ-042 | Fault Handling | Caller-initiated cancellation (e.g. closing the UI) shall be distinguished from a genuine communication failure and propagated as a normal cancellation, not reported as an error. |
| REQ-043 | Fault Handling | An unsolicited `PROGRESS` or `COMPLETE` line arriving ahead of a command's actual reply shall be skipped over correctly, not mistaken for that reply. |
| REQ-044 | Fault Handling | A malformed or unrecognized response to a sent command shall be handled as an Unknown response without corrupting workflow state. |

## UI

| ID | Category | Requirement |
|---|---|---|
| REQ-050 | UI | The UI shall allow the user to choose between a serial (COM port) and a TCP connection before connecting. |
| REQ-051 | UI | The UI shall reflect transport-open status separately from workflow state, since a device can be reachable without speaking the application protocol. |
| REQ-052 | UI | The UI shall enable or disable each action button according to the workflow state that permits it, in addition to — not instead of — the workflow's own enforcement. |
| REQ-053 | UI | The UI shall display an append-only, timestamped event log of actions sent and responses received. |
| REQ-054 | UI | The UI shall display live progress (percent complete) while a treatment is Running, without requiring further user action. |
| REQ-055 | UI | The UI shall release the transport and stop any background update activity when the session is disconnected or the window is closed. |
