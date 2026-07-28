using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace AiOrchestrator.EndToEndTests;

/// <summary>
/// A stand-in for the GitHub REST API, stubbed at the <b>HTTP boundary</b> rather than at the
/// <c>IBacklogConnector</c> seam.
/// <para>
/// That choice is the point. The functional tier already stubs the seam, so it never runs Octokit;
/// if E2E stubbed the same place, nothing would ever exercise the client, its base-address
/// handling, or its deserialisation — and the first thing to find a defect there would be
/// production. Here the host runs its real connector, real Octokit, real HTTP; only the far end is
/// ours. And it needs no GitHub token, so CI can run it (task 7.4).
/// </para>
/// </summary>
public sealed class GitHubStub : IAsyncDisposable
{
    readonly HttpListener _listener = new();
    readonly CancellationTokenSource _stopping = new();
    readonly List<string> _requests = [];

    Task? _loop;

    public GitHubStub()
    {
        BaseAddress = new Uri($"http://127.0.0.1:{FreePort()}/");

        // Octokit treats any host other than api.github.com as GitHub Enterprise Server and
        // appends "api/v3/" — so the stub answers there, which is exactly the path a real
        // Enterprise deployment would take.
        _listener.Prefixes.Add(BaseAddress.ToString());
    }

    /// <summary>Pass to the host as <c>Backlog:GitHub:BaseAddress</c>.</summary>
    public Uri BaseAddress { get; }

    /// <summary>Issues the stub reports.</summary>
    public List<StubIssue> Issues { get; } = [];

    /// <summary>Repositories the stub knows about, as "owner/name". Anything else answers 404.</summary>
    public HashSet<string> Repositories { get; } = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Requests
    {
        get
        {
            lock (_requests)
            {
                return [.. _requests];
            }
        }
    }

    public void Start()
    {
        _listener.Start();
        _loop = Task.Run(Serve);
    }

    async Task Serve()
    {
        while (!_stopping.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (HttpListenerException)
            {
                return; // The listener was stopped.
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            try
            {
                Respond(context);
            }
            finally
            {
                context.Response.Close();
            }
        }
    }

    void Respond(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath ?? string.Empty;

        lock (_requests)
        {
            _requests.Add(path);
        }

        // /api/v3/repos/{owner}/{repo}[/issues]
        var segments = path.Trim('/').Split('/');
        if (segments is not ["api", "v3", "repos", var owner, var repository, ..])
        {
            Write(context, HttpStatusCode.NotFound, """{"message":"Not Found"}""");
            return;
        }

        if (!Repositories.Contains($"{owner}/{repository}"))
        {
            Write(context, HttpStatusCode.NotFound, """{"message":"Not Found"}""");
            return;
        }

        if (segments.Length == 5)
        {
            Write(
                context,
                HttpStatusCode.OK,
                JsonSerializer.Serialize(
                    new
                    {
                        id = 1,
                        name = repository,
                        full_name = $"{owner}/{repository}",
                        owner = new { login = owner, id = 1 },
                    }
                )
            );
            return;
        }

        if (segments is [.., "issues"])
        {
            Write(context, HttpStatusCode.OK, IssuesJson());
            return;
        }

        // The licensed write (UC-008), as Octokit performs it:
        //   POST   /issues/{number}/labels          — add-to-set
        //   DELETE /issues/{number}/labels/{name}   — remove
        // The stub applies it to its own issue list, so a labelled Story really is labelled at
        // the far end and ordinary reconciliation carries it back — which is what makes the
        // board's chain assertion (#110) a fact rather than a mock's opinion.
        // Matched positionally: "issues" then the number then "labels", optionally followed by
        // the label name on a DELETE. A second slice pattern is not allowed in one list pattern.
        var labelsAt = Array.IndexOf(segments, "labels");
        if (
            labelsAt >= 2
            && segments[labelsAt - 2] == "issues"
            && int.TryParse(segments[labelsAt - 1], out var number)
        )
        {
            var issue = Issues.FirstOrDefault(candidate => candidate.Number == number);
            if (issue is null)
            {
                Write(context, HttpStatusCode.NotFound, """{"message":"Not Found"}""");
                return;
            }

            if (context.Request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(context.Request.InputStream);
                var applied = JsonSerializer.Deserialize<string[]>(reader.ReadToEnd()) ?? [];
                Replace(issue, [.. issue.Labels.Union(applied, StringComparer.Ordinal)]);
                Write(context, HttpStatusCode.OK, "[]");
                return;
            }

            if (context.Request.HttpMethod == "DELETE" && segments.Length > labelsAt + 1)
            {
                var removed = Uri.UnescapeDataString(segments[labelsAt + 1]);
                Replace(issue, [.. issue.Labels.Where(label => label != removed)]);
                Write(context, HttpStatusCode.OK, "[]");
                return;
            }
        }

        // GET /labels/{name}: the connector asks before creating (EnsureLabel).
        if (segments is [.., "labels", _])
        {
            Write(context, HttpStatusCode.OK, """{"id":1,"name":"label","color":"ededed"}""");
            return;
        }

        Write(context, HttpStatusCode.NotFound, """{"message":"Not Found"}""");
    }

    /// <summary>StubIssue is a record; a label change replaces it in place, keeping order.</summary>
    void Replace(StubIssue issue, string[] labels)
    {
        var index = Issues.IndexOf(issue);
        if (index >= 0)
        {
            Issues[index] = issue with { Labels = labels };
        }
    }

    string IssuesJson() =>
        JsonSerializer.Serialize(
            Issues.Select(issue => new
            {
                id = issue.Number,
                number = issue.Number,
                title = issue.Title,
                state = issue.State,
                body = issue.Body,
                labels = issue.Labels.Select(label => new
                {
                    id = 1,
                    name = label,
                    color = "ededed",
                }),
            })
        );

    static void Write(HttpListenerContext context, HttpStatusCode status, string json)
    {
        var payload = Encoding.UTF8.GetBytes(json);

        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = payload.Length;
        context.Response.OutputStream.Write(payload);
    }

    static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        _listener.Close();

        if (_loop is not null)
        {
            await _loop;
        }

        _stopping.Dispose();
    }
}

/// <summary>One issue as the stub reports it — a record rather than a tuple so a new field
/// (the body arrived with #37) reads at the call site instead of being a positional guess.</summary>
public sealed record StubIssue(
    int Number,
    string Title,
    string State,
    string[] Labels,
    string? Body = null
);
