# Spike: an HTML5 terminal on an sbx sandbox

Probed 2026-08-10 on the developer's Mac (sbx from `~/.local/bin/sbx`, daemon running, sandbox
`spike-term` on the `shell` template over the ai-orchestrator workspace).

## Answered

**H1 — `sbx exec -it` needs a tty on THIS side, and a redirected .NET pipe is not one.**

| probe | result |
|---|---|
| `printf … \| sbx exec -i spike-term bash` | works, but `tty` answers **`not a tty`** — a line pipe, no signals, no cursor addressing |
| `printf … \| sbx exec -it spike-term bash` | **fails**: `ERROR: inspect exec: context deadline exceeded` |
| `script -q /dev/null sbx exec -it spike-term bash -c 'tty'` | **`/dev/pts/1`** — a real pty |

So the tty has to be allocated on the host before sbx is spawned. `script` proves the mechanic with
zero dependencies; the real implementation wants an openpty (`Pty.Net` or a P/Invoke) because of H3.

**H2 — a raw byte pipe over a WebSocket drives xterm.js faithfully.** Verified in the browser
against the running spike:

- live `bash -i` prompt, `uname -s` → `Linux` (the guest kernel, not the host)
- the Run's workspace is mounted and visible (`AGENTS.md openspec src`), with `ls` colour intact
- `stty size` → `58 128`, matching the browser's `FitAddon` measurement
- **`^C` interrupted a running `sleep 300` and returned the prompt** — SIGINT through the pty, the
  one thing a pipe cannot do
- `top` renders full-screen: reverse-video header, live refresh, correct geometry

Binary frames, not text: a UTF-8 sequence split across two reads corrupts as text, while
`term.write(Uint8Array)` reassembles it.

**H3 — resize is the one thing `script` cannot do.** It does not propagate a window size, so the
sandbox pty is 0x0 unless something sets it: `stty size` answered `0 0` until the spike set it
explicitly with `stty rows N cols M` inside the exec. That fixes the size **at connect time only**.
Live resize needs the `TIOCSWINSZ` ioctl on the pty master, which `script` does not expose — this
is the reason to prefer a real openpty over the `script` trick in the product.

## Corrected mid-spike

An early probe looked like "the Enter key does nothing, so the pty lacks `icrnl`". It was the test
harness: the browser tool's synthetic `Return` never reached xterm's textarea (an `onData` hook
recorded nothing). A CR pushed straight down the socket executes the line normally. No product
defect — worth recording because the false version was one comment away from being designed around.

## Not answered (deliberately out of scope)

- **Which sandbox.** The spike hardcodes a name. In the product the name is a local variable in
  `SbxAgentProcessHost.Run`; a `RunSandboxHost` registry modelled on `RunPreviewHost` is the fix.
- **Who may.** No authentication at all here. A terminal is arbitrary command execution in a
  sandbox carrying the machine owner's session (#288) — it needs its own grant, not
  `RunPermissions.Read`, checked in the hub the way `RunLogHub.Watch` does.
- **What is recorded.** Nothing. A human's commands inside a Run's sandbox are part of what that
  Run did, and the transcript currently cannot express them.

## Reproduce

```bash
sbx create --name spike-term shell /Users/andoniplain/projects/charlas/ai-orchestrator
SPIKE_SANDBOX=spike-term dotnet run TerminalSpike.cs   # then open http://127.0.0.1:5099
```
