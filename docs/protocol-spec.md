# Protocol Specification

The application-level protocol spoken between `TreatmentSession` (host side)
and a device — either the real serial hardware (if it understood this
protocol, which the stock Flipper Zero CLI does not) or `SimulatedDeviceServer`
(`MedDeviceSim.Simulator`), which implements it statefully over TCP as an
independent, second implementation.

This document describes the wire protocol itself, independent of which
transport (serial or TCP) carries it — see `ITransport` in
`MedDeviceSim.Communication` for the transport abstraction.

## Framing (REQ-030)

- Line-based: every command and response is a single line terminated with `\r\n`.
- ASCII-encoded.
- A single line may arrive split across multiple transport reads, and multiple
  lines may arrive combined in a single read; both are reassembled correctly
  by `LineReader` (REQ-035) before either side sees a complete line.
- Both transport implementations enforce a 2-second read/write timeout — a
  side that goes silent for longer than that is treated as a communication
  failure (REQ-040), not left to hang indefinitely.

## Commands (host → device)

| Command | Wire format | Valid from state | Response(s) |
|---|---|---|---|
| Connect | `CONNECT\r\n` | Disconnected | `CONNECTED` |
| LoadPlan | `LOAD_PLAN <id>\r\n` | Connected | `PLAN_LOADED <id>` |
| Arm | `ARM\r\n` | PlanLoaded | `READY` |
| Start | `START\r\n` | Armed | `RUNNING`, then unprompted `PROGRESS <percent>` (repeated) and `COMPLETE` |
| Stop | `STOP\r\n` | Armed or Running | `STOPPED` |
| GetStatus | `GET_STATUS\r\n` | — | not implemented by either side (see [Known gaps](#known-gaps)) |

"Valid from state" is enforced independently on both sides of the wire:
`TreatmentWorkflow` rejects an invalid request locally before it is ever sent
(REQ-016), and `SimulatedDeviceServer.HandleLine` independently rejects the
same invalid commands if one somehow arrived, replying with `ERROR` (see
below) rather than silently accepting it.

## Responses (device → host)

| Response | Wire format | Meaning | Can arrive unprompted? |
|---|---|---|---|
| Connected | `CONNECTED` | Accepts `CONNECT` | No |
| PlanLoaded | `PLAN_LOADED <id>` | Accepts `LOAD_PLAN` | No |
| Ready | `READY` | Accepts `ARM` | No |
| Running | `RUNNING` | Accepts `START` | No |
| Progress | `PROGRESS <percent>` | Treatment progress while Running | **Yes** |
| Complete | `COMPLETE` | Treatment finished | **Yes** |
| Stopped | `STOPPED` | Accepts `STOP` | No |
| Error | `ERROR <reason>` | Command rejected or invalid at the device | No |

`Progress` and `Complete` are the only two response kinds that may arrive on
their own schedule, not as a direct reply to whatever was most recently sent
(REQ-034) — `SimulatedDeviceServer` starts writing these autonomously right
after replying `RUNNING` to `START`. Every other response kind is guaranteed
to be a direct reply to the command that immediately preceded it. This is
exactly why `TreatmentSession.ReadCommandReplyAsync` has to loop past
unsolicited `Progress`/`Complete` lines to find a command's actual reply
(REQ-043) — see the checkpoint that fixed `StopAsync` consuming a queued
`PROGRESS` line instead of `STOP`'s own reply.

### Error reason format

`SimulatedDeviceServer`'s reference format for a rejected/invalid line is:

```
ERROR Cannot process '<line>' while <state>
```

e.g. `ERROR Cannot process 'ARM' while Disconnected`. This exact wording is
the simulator's own choice, not a protocol requirement — `DeviceResponse.Error`
only requires *some* non-empty reason text after the `ERROR` keyword; host
code must not depend on the reason string's specific wording, only its
presence.

## Unrecognized input

A line that doesn't match any of the shapes above — on either side — parses
to a distinct `Unknown` response/command rather than throwing (REQ-033). This
is the expected outcome when talking to the stock Flipper CLI, which
understands none of these commands and replies with its own banners, prompts,
and error text instead.

## Known gaps

- **`GET_STATUS` is defined but not implemented anywhere.** `DeviceCommand.GetStatus`
  produces a valid wire line, but `TreatmentWorkflow` has no request method for
  it, and `SimulatedDeviceServer.HandleLine` has no matching case (so a real
  send would just fall through to `ERROR`). Tracked as a known limitation in
  the README, not resolved by this spec.
