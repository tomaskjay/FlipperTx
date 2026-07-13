# State Machine

`TreatmentWorkflow` (`MedDeviceSim.Workflow`) is the sole authority for valid
treatment state. It is pure and synchronous — no I/O, no async — so every
rule below is enforced independent of any transport, UI, or timing (REQ-010).

## States

| State | Data carried |
|---|---|
| `Disconnected` | — |
| `Connected` | — |
| `PlanLoaded` | `PlanId` |
| `Armed` | `PlanId` |
| `Running` | `PlanId`, `PercentComplete` |
| `Complete` | `PlanId` |
| `Stopped` | `PlanId` |
| `Fault` | `Reason` (not `PlanId` — see [Notes](#notes)) |

## Diagram

```text
Disconnected --CONNECTED--> Connected --PLAN_LOADED--> PlanLoaded --READY--> Armed --RUNNING--> Running --COMPLETE--> Complete
                                                                       │                 │
                                                                       └────STOPPED──────┴──────────────────────────> Stopped

any state except Disconnected --device ERROR response--> Fault
any state --explicit disconnect or communication failure--> Disconnected
```

Only a device-reported `ERROR` *response* — the device replying while the
connection itself is still working — leads to `Fault` (REQ-018). Everything
else that goes wrong (a timeout, an I/O error, an unexpected disconnect —
anything where the device stops being reachable at all) forces `Disconnected`
instead, from *anywhere*, including from `Fault`, `Complete`, or `Stopped`
(REQ-017). The distinction is deliberate: `Fault` means the device told us
something is wrong; `Disconnected` means we lost the ability to ask it
anything, so we cannot honestly claim to know its state.

## Two kinds of transition

`TreatmentWorkflow` has two separate transition mechanisms, and they matter
for different reasons:

### 1. Request validation (`Request*` methods) — synchronous, local, no state change yet

Calling `RequestConnect()`, `RequestLoadPlan()`, `RequestArm()`,
`RequestStart()`, or `RequestStop()` only checks whether the action is valid
*from the current state* and returns either `Accepted` (with the
`DeviceCommand` to send) or `Rejected` (with a reason) — `CurrentState` does
not change here. This is what lets `TreatmentSession` reject an invalid
action before any transport I/O happens (REQ-016).

| Request | Valid from | REQ |
|---|---|---|
| `RequestConnect` | `Disconnected` | REQ-011 |
| `RequestLoadPlan` | `Connected` | REQ-012 |
| `RequestArm` | `PlanLoaded` | REQ-013 |
| `RequestStart` | `Armed` | REQ-014 |
| `RequestStop` | `Armed` or `Running` | REQ-015 |

### 2. Response application (`OnResponse`) — the actual state change

`CurrentState` only changes once `OnResponse` is fed a `DeviceResponse` —
i.e. once the device (real or simulated) has confirmed the action actually
happened. This is the full set of `(CurrentState, Response) → NewState`
transitions `TreatmentWorkflow.OnResponse` implements:

| From state | Response | To state |
|---|---|---|
| `Disconnected` | `Connected` | `Connected` |
| `Connected` | `PlanLoaded(id)` | `PlanLoaded(id)` |
| `PlanLoaded(id)` | `Ready` | `Armed(id)` |
| `Armed(id)` | `Running` | `Running(id, 0)` |
| `Running(id, _)` | `Progress(p)` | `Running(id, p)` |
| `Running(id, _)` | `Complete` | `Complete(id)` |
| `Armed(id)` | `Stopped` | `Stopped(id)` |
| `Running(id, _)` | `Stopped` | `Stopped(id)` |
| any state except `Disconnected` | `Error(reason)` | `Fault(reason)` |
| anything else | — | unchanged (REQ-019) |

The plan ID threads through every one of these transitions unmodified
(REQ-020) — it's established once at `PLAN_LOADED` and carried forward
through `Armed`, `Running`, `Complete`, and `Stopped` without being
re-specified.

### Forced transitions (not driven by a response at all)

- `OnDisconnected()` — called by `TreatmentSession` on **every** communication
  failure (a timeout, an I/O error, or an unexpected disconnect, whether
  during a request's own send/read or otherwise), and on explicit
  disconnect/dispose. Unconditionally sets `CurrentState` to `Disconnected`,
  regardless of the prior state (REQ-017) — see
  `TreatmentSession.ExecuteAsync`/`ReadAndProcessOneResponseAsync`, both of
  which call this, never `OnResponse`, when the transport itself fails.
  `Fault` is never reached this way.

## Notes

- **`Fault` does not carry the plan ID.** Unlike `Complete`/`Stopped`, which
  keep `PlanId`, `Fault` only carries `Reason`. If a fault happens
  mid-treatment, which plan was running is not recoverable from
  `CurrentState` alone — only from the event log. Not currently treated as a
  problem, since no requirement depends on it, but worth knowing if that
  changes.
- **There is no transition out of `Complete`, `Stopped`, or `Fault` other than
  a full disconnect.** `RequestConnect` is only valid from `Disconnected`
  (REQ-011), so starting another plan after finishing, stopping, or faulting
  requires disconnecting and reconnecting — there is no "reset and stay
  connected" path today. Reflects current UI behavior (no button does this)
  more than a deliberate protocol rule.
