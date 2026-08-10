#:sdk Microsoft.NET.Sdk.Web

// Throwaway spike: is an HTML5 terminal on an sbx sandbox reachable end to end?
// One hardcoded sandbox, a bare WebSocket, NO authentication. Not shippable — it exists to
// answer three questions before any design commits to them:
//
//   Q1  can a .NET process give `sbx exec -it` the tty it demands?     (probed: yes, via script)
//   Q2  does a byte pipe over a WebSocket drive xterm.js faithfully?   (this)
//   Q3  what does resize cost?                                        (this — see stty below)
//
// Run with:  SPIKE_SANDBOX=spike-term dotnet run TerminalSpike.cs
// Then open: http://127.0.0.1:5099

using System.Diagnostics;
using System.Net.WebSockets;

var sandbox = Environment.GetEnvironmentVariable("SPIKE_SANDBOX") ?? "spike-term";
var sbx =
    Environment.GetEnvironmentVariable("SBX_PATH")
    ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".local/bin/sbx"
    );

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseWebSockets();

app.MapGet("/", () => Results.Content(Html.Page, "text/html"));

app.MapGet(
    "/ws",
    async (HttpContext context) =>
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            return Results.BadRequest("websocket only");
        }

        var cols = int.TryParse(context.Request.Query["cols"], out var c) ? c : 80;
        var rows = int.TryParse(context.Request.Query["rows"], out var r) ? r : 24;

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        await Pump(socket, cols, rows, context.RequestAborted);
        return Results.Empty;
    }
);

app.Run("http://127.0.0.1:5099");

async Task Pump(WebSocket socket, int cols, int rows, CancellationToken cancellationToken)
{
    // `script` is what allocates the tty. sbx exec -it refuses a plain pipe outright ("inspect
    // exec: context deadline exceeded", probed 2026-08-10), and a .NET child process's redirected
    // stdin is exactly that pipe — so something has to open a pty on this side first. In the real
    // thing this is an openpty (Pty.Net or a P/Invoke); here it is /usr/bin/script, which is the
    // same idea with zero dependencies.
    //
    // The size is set INSIDE, with stty, because script does not propagate a window size: without
    // this the sandbox's pty is 0x0 and every full-screen program draws into nothing (probed:
    // `stty size` answered "0 0"). It also means resize is one-shot for this spike — the browser's
    // size at connect time. Live resize needs the openpty, whose ioctl this cannot reach.
    var psi = new ProcessStartInfo("/usr/bin/script")
    {
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };

    foreach (
        var argument in new[]
        {
            "-q",
            "/dev/null",
            sbx,
            "exec",
            "-it",
            sandbox,
            "bash",
            "-lc",
            // `icrnl` is stated rather than assumed: xterm.js sends CR for Enter, and a shell
            // needs NL. The pty appears to map it anyway (a CR pushed straight down the socket
            // executed the line), so this is belt-and-braces on a termios `script` sets up for us
            // rather than a fix for anything observed. Named here so the next reader knows the
            // question was asked.
            //
            // If CR ever does need translating, translate it HERE and not in the transport: a
            // transport that rewrites keystrokes would also have to decide what a program in raw
            // mode meant by CR, and it cannot know.
            $"stty rows {rows} cols {cols} icrnl; exec bash -i",
        }
    )
    {
        psi.ArgumentList.Add(argument);
    }

    using var shell = Process.Start(psi)!;
    Console.WriteLine($"[spike] pty open on '{sandbox}' at {cols}x{rows} (pid {shell.Id})");

    using var ended = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

    // Both directions are raw bytes. Nothing here parses, buffers by line, or re-encodes: xterm.js
    // is the terminal emulator and this is the wire. Binary frames on purpose — a UTF-8 sequence
    // split across two reads would corrupt as text, while xterm.write(Uint8Array) reassembles it.
    var outward = Copy(shell.StandardOutput.BaseStream, socket, ended.Token);
    var errward = Copy(shell.StandardError.BaseStream, socket, ended.Token);
    var inward = Feed(socket, shell.StandardInput.BaseStream, ended.Token);

    await Task.WhenAny(outward, errward, inward, shell.WaitForExitAsync(ended.Token));
    await ended.CancelAsync();

    if (!shell.HasExited)
    {
        // The tree, not the child: killing `script` alone would orphan the sbx CLI holding the exec.
        shell.Kill(entireProcessTree: true);
    }

    Console.WriteLine($"[spike] pty closed on '{sandbox}'");
}

static async Task Copy(Stream from, WebSocket socket, CancellationToken cancellationToken)
{
    var buffer = new byte[8192];

    try
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await from.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return;
            }

            await socket.SendAsync(
                buffer.AsMemory(0, read),
                WebSocketMessageType.Binary,
                endOfMessage: true,
                cancellationToken
            );
        }
    }
    catch (Exception exception)
        when (exception is OperationCanceledException or IOException or WebSocketException)
    {
        // The shell exited or the tab closed. Both are how a terminal ends, not faults.
    }
}

static async Task Feed(WebSocket socket, Stream into, CancellationToken cancellationToken)
{
    var buffer = new byte[4096];

    try
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var received = await socket.ReceiveAsync(buffer, cancellationToken);
            if (received.MessageType == WebSocketMessageType.Close)
            {
                return;
            }

            await into.WriteAsync(buffer.AsMemory(0, received.Count), cancellationToken);
            await into.FlushAsync(cancellationToken);
        }
    }
    catch (Exception exception)
        when (exception is OperationCanceledException or IOException or WebSocketException)
    {
        // Same: a closed tab is not an error.
    }
}

// Deliberately one file, CDN-loaded, no build step: the spike must be deletable in one `rm`.
static class Html
{
    public const string Page = """
<!doctype html>
<html>
<head>
  <meta charset="utf-8" />
  <title>sbx terminal spike</title>
  <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/@xterm/xterm@5.5.0/css/xterm.min.css" />
  <script src="https://cdn.jsdelivr.net/npm/@xterm/xterm@5.5.0/lib/xterm.js"></script>
  <script src="https://cdn.jsdelivr.net/npm/@xterm/addon-fit@0.10.0/lib/addon-fit.js"></script>
  <style>
    html, body { margin: 0; height: 100%; background: #0b0e14; }
    #host { height: 100%; padding: 8px; box-sizing: border-box; }
    #status { position: fixed; right: 10px; top: 8px; font: 12px ui-monospace, monospace; color: #6b7280; }
  </style>
</head>
<body>
  <div id="status">connecting…</div>
  <div id="host"></div>
  <script>
    const status = document.getElementById("status");
    const term = new Terminal({
      fontSize: 13,
      fontFamily: "ui-monospace, SFMono-Regular, Menlo, monospace",
      cursorBlink: true,
      theme: { background: "#0b0e14", foreground: "#e5e7eb" },
    });
    const fit = new FitAddon.FitAddon();
    term.loadAddon(fit);
    term.open(document.getElementById("host"));
    fit.fit();

    // Size travels at connect time only — see the stty comment server-side for why.
    const ws = new WebSocket(`ws://${location.host}/ws?cols=${term.cols}&rows=${term.rows}`);
    ws.binaryType = "arraybuffer";

    ws.onopen = () => { status.textContent = "live"; status.style.color = "#22c55e"; term.focus(); };
    ws.onmessage = (event) => term.write(new Uint8Array(event.data));
    ws.onclose = () => {
      status.textContent = "closed";
      status.style.color = "#ef4444";
      term.write("\r\n\x1b[31m[sandbox connection closed]\x1b[0m\r\n");
    };

    term.onData((data) => {
      if (ws.readyState === WebSocket.OPEN) ws.send(new TextEncoder().encode(data));
    });

    addEventListener("resize", () => fit.fit());
  </script>
</body>
</html>
""";
}
