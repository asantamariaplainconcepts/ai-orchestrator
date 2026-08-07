// H4 — can a .NET process drive sbx the way PodRunLauncher drives the docker socket?
// Three outcomes must stay distinguishable (the #279 remedy pattern needs the third named):
//   1. success            → exit 0, stdout captured
//   2. work failed        → non-zero exit from inside the sandbox, stderr captured
//   3. launcher refusal   → sbx itself absent/broken/asked the impossible
// Run with: dotnet run poc/SbxHarness.cs   (file-based app, .NET 10)

using System.Diagnostics;

var sbx =
    Environment.GetEnvironmentVariable("SBX_PATH")
    ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".local/bin/sbx"
    );
var name = $"spike-h4-{Guid.NewGuid().ToString("N")[..8]}";
var workspace = Directory.CreateTempSubdirectory("sbx-h4-").FullName;
var failures = 0;

try
{
    // 1 — success path: create (detached), exec, capture, remove.
    var create = Run(sbx, "run", "-d", "--name", name, "shell", workspace);
    Check("create exits 0", create.ExitCode == 0, create.Stderr);

    var exec = Run(sbx, "exec", name, "sh", "-c", "echo out-marker; uname -s");
    Check("exec exits 0", exec.ExitCode == 0, exec.Stderr);
    Check("stdout captured", exec.Stdout.Contains("out-marker"), exec.Stdout);
    Check("it is the guest kernel", exec.Stdout.Contains("Linux"), exec.Stdout);

    // 2 — work failed: non-zero from inside must surface as non-zero + stderr, like
    //     WaitContainerAsync's StatusCode + Logs do today.
    var boom = Run(sbx, "exec", name, "sh", "-c", "echo boom-detail >&2; exit 3");
    Check("inner failure exit code travels", boom.ExitCode == 3, $"got {boom.ExitCode}");
    Check("stderr captured", boom.Stderr.Contains("boom-detail"), boom.Stderr);

    // 3a — launcher refusal, daemon reachable: impossible request names itself.
    var ghost = Run(sbx, "exec", "no-such-sandbox-xyz", "true");
    Check(
        "unknown sandbox refused",
        ghost.ExitCode != 0 && ghost.ExitCode != 3,
        $"got {ghost.ExitCode}"
    );
    Check("refusal names the cause", ghost.Stderr.Contains("no-such-sandbox-xyz"), ghost.Stderr);

    // 3b — launcher refusal, sbx absent: the Win32Exception arm — distinguishable from any
    //      exit code because the process never starts.
    try
    {
        Run("/nonexistent/sbx", "ls");
        Check("absent binary throws", false, "no exception");
    }
    catch (System.ComponentModel.Win32Exception e)
    {
        Check("absent binary throws Win32Exception", true, e.Message);
    }
}
finally
{
    var rm = Run(sbx, "rm", "--force", name); // --force: sbx refuses prompts on a non-tty
    Check("rm --force from non-tty", rm.ExitCode == 0, rm.Stderr);
    Directory.Delete(workspace, recursive: true);
}

Console.WriteLine(
    failures == 0 ? "\nH4 HARNESS: all checks passed" : $"\nH4 HARNESS: {failures} FAILED"
);
return failures == 0 ? 0 : 1;

void Check(string what, bool ok, string detail)
{
    failures += ok ? 0 : 1;
    Console.WriteLine($"  {(ok ? "ok " : "FAIL")} {what}{(ok ? "" : $" — {Trim(detail)}")}");
}

static string Trim(string s) => s.Length <= 200 ? s.Trim() : s[..200].Trim() + " …";

static Result Run(string file, params string[] args)
{
    var psi = new ProcessStartInfo(file)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    foreach (var a in args)
        psi.ArgumentList.Add(a);
    using var p = Process.Start(psi)!;
    var stdout = p.StandardOutput.ReadToEndAsync();
    var stderr = p.StandardError.ReadToEndAsync();
    if (!p.WaitForExit(TimeSpan.FromMinutes(5)))
    {
        p.Kill(entireProcessTree: true);
        throw new TimeoutException(file);
    }
    return new Result(p.ExitCode, stdout.Result, stderr.Result);
}

sealed record Result(int ExitCode, string Stdout, string Stderr);
