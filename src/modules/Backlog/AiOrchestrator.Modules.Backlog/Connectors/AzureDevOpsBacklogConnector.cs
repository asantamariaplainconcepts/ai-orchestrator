using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AiOrchestrator.Modules.Backlog.Domain;
using ErrorOr;

namespace AiOrchestrator.Modules.Backlog.Connectors;

/// <summary>
/// Azure DevOps — the second vendor, and therefore the thing that proves the seam is a seam
/// rather than a GitHub abstraction wearing a neutral name (DEC-011, design D1).
/// <para>
/// <b>UNEXERCISED (ADR-0005).</b> No call in this file has ever reached a real Azure DevOps
/// organisation; none was available. The translation below is unit-tested and the seam contract
/// is exercised through the stub tier, but "the REST calls behave as documented" is a
/// hypothesis. First thing to try when an organisation exists: configure a Connector against a
/// project and hit refresh — that one path exercises authentication, the work-item query, and
/// tag/state/description translation together.
/// </para>
/// <para>
/// Plain HTTP rather than the Azure DevOps client SDK, deliberately: an SDK that cannot be
/// exercised is a large dependency whose behaviour is equally unverified, while a thin client
/// over documented endpoints is small, fully unit-testable, and obvious to correct.
/// </para>
/// </summary>
sealed class AzureDevOpsBacklogConnector(IAzureDevOpsClientFactory clientFactory)
    : IBacklogConnector
{
    /// <summary>Pinned like the Storage API version was, and for the same reason (#16).</summary>
    public const string ApiVersion = "7.1";

    /// <summary>
    /// The estimate lives in a different field per process template — Agile, Scrum, and Basic
    /// which has none. Tried in order; when none applies the failure names them (design D3).
    /// </summary>
    public static readonly string[] EstimateFields =
    [
        "Microsoft.VSTS.Scheduling.StoryPoints",
        "Microsoft.VSTS.Scheduling.Effort",
    ];

    public BacklogVendor Vendor => BacklogVendor.AzureDevOps;

    public async Task<CredentialVerdict> VerifyAccess(
        BacklogCoordinates coordinates,
        string documentPath,
        string token,
        CancellationToken cancellationToken
    )
    {
        // Same shape as the GitHub implementation and, deliberately, not the same calls: this
        // vendor's permission model is its own, which is exactly what a verdict per capability
        // keeps inside this file (#132, design D2).
        var stories = await Probe(
            Capabilities.Stories,
            () => FetchStories(coordinates, token, cancellationToken)
        );

        var documents = await Probe(
            Capabilities.Documents,
            () => ReadDocument(coordinates, documentPath, "HEAD", token, cancellationToken),
            // Absence is not refusal (design D6). This connector reports a missing document as
            // DocumentNotFound, so that code — and only that one — passes.
            absent: BacklogErrors.DocumentNotFound(documentPath).Code
        );

        return CredentialVerdict.Of(stories, documents);
    }

    /// <summary>
    /// One capability, attempted through the connector's own read so the probe and the real call
    /// cannot diverge in how they authenticate or how they translate a refusal.
    /// </summary>
    static async Task<CapabilityResult> Probe<T>(
        string capability,
        Func<Task<ErrorOr<T>>> read,
        string? absent = null
    )
    {
        var result = await read();
        if (!result.IsError)
        {
            return CapabilityResult.Passed(capability);
        }

        var failure = result.FirstError;
        return absent is not null && failure.Code == absent
            ? CapabilityResult.Passed(capability)
            : CapabilityResult.Refused(capability, failure);
    }

    public async Task<ErrorOr<BacklogSnapshot>> FetchStories(
        BacklogCoordinates coordinates,
        string token,
        CancellationToken cancellationToken
    )
    {
        var client = clientFactory.Create(coordinates.Owner, token);

        return await Guarded<BacklogSnapshot>(
            coordinates,
            async () =>
            {
                // WIQL gives ids; the batch read gives fields. Two calls is the documented
                // shape — there is no "query returning fields" endpoint.
                var query = new
                {
                    query = "SELECT [System.Id] FROM WorkItems "
                        + "WHERE [System.TeamProject] = @project "
                        + "AND [System.State] NOT IN ('Closed', 'Done', 'Removed') "
                        + "ORDER BY [System.Id]",
                };

                var wiql = await client.PostAsJsonAsync(
                    $"{Uri.EscapeDataString(coordinates.Repository)}/_apis/wit/wiql?api-version={ApiVersion}",
                    query,
                    cancellationToken
                );

                if (Translate(wiql, coordinates) is { } failure)
                {
                    return failure;
                }

                var ids = await ReadIds(wiql, cancellationToken);
                if (ids.Count == 0)
                {
                    return (ErrorOr<BacklogSnapshot>)new BacklogSnapshot([]);
                }

                var batch = await client.GetAsync(
                    $"_apis/wit/workitems?ids={string.Join(',', ids)}&$expand=all&api-version={ApiVersion}",
                    cancellationToken
                );

                if (Translate(batch, coordinates) is { } batchFailure)
                {
                    return batchFailure;
                }

                using var document = JsonDocument.Parse(
                    await batch.Content.ReadAsStringAsync(cancellationToken)
                );

                return (ErrorOr<BacklogSnapshot>)
                    new BacklogSnapshot([
                        .. document
                            .RootElement.GetProperty("value")
                            .EnumerateArray()
                            .Select(ToStory),
                    ]);
            }
        );
    }

    public async Task<ErrorOr<VendorStory?>> FetchStory(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        string token,
        CancellationToken cancellationToken
    )
    {
        var client = clientFactory.Create(coordinates.Owner, token);

        return await Guarded<VendorStory?>(
            coordinates,
            async () =>
            {
                var response = await client.GetAsync(
                    $"_apis/wit/workitems/{vendorStoryId}?$expand=all&api-version={ApiVersion}",
                    cancellationToken
                );

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return (ErrorOr<VendorStory?>)(VendorStory?)null;
                }

                if (Translate(response, coordinates) is { } failure)
                {
                    return failure;
                }

                using var document = JsonDocument.Parse(
                    await response.Content.ReadAsStringAsync(cancellationToken)
                );

                return (ErrorOr<VendorStory?>)ToStory(document.RootElement);
            }
        );
    }

    /// <summary>
    /// A deliberate no-op. Azure DevOps tags are not repository objects — there is no tag until
    /// one is applied to a work item, and the only way to "create" one would be to tag somebody
    /// else's backlog item to satisfy our own bookkeeping. Succeeding without acting is the
    /// honest answer; the caller reports to the Admin that labels were not ensured, so the
    /// asymmetry is visible rather than implied (automation-defaults design D3).
    /// </summary>
    public Task<ErrorOr<Success>> EnsureLabel(
        BacklogCoordinates coordinates,
        string label,
        string token,
        CancellationToken cancellationToken
    ) => Task.FromResult<ErrorOr<Success>>(Result.Success);

    public Task<ErrorOr<Success>> ApplyLabel(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        string label,
        string token,
        CancellationToken cancellationToken
    ) => WithTags(coordinates, vendorStoryId, token, tags => tags.Append(label), cancellationToken);

    public Task<ErrorOr<Success>> RemoveLabel(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        string label,
        string token,
        CancellationToken cancellationToken
    ) =>
        WithTags(
            coordinates,
            vendorStoryId,
            token,
            tags =>
                tags.Where(tag => !string.Equals(tag, label, StringComparison.OrdinalIgnoreCase)),
            cancellationToken
        );

    public async Task<ErrorOr<Success>> AddComment(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        string comment,
        string token,
        CancellationToken cancellationToken
    )
    {
        var client = clientFactory.Create(coordinates.Owner, token);

        return await Guarded<Success>(
            coordinates,
            async () =>
            {
                var response = await client.PostAsJsonAsync(
                    $"{Uri.EscapeDataString(coordinates.Repository)}/_apis/wit/workItems/"
                        + $"{vendorStoryId}/comments?api-version={ApiVersion}-preview.3",
                    new { text = comment },
                    cancellationToken
                );

                return Translate(response, coordinates) is { } failure
                    ? failure
                    : (ErrorOr<Success>)Result.Success;
            }
        );
    }

    public async Task<ErrorOr<Success>> SetState(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        string state,
        string token,
        CancellationToken cancellationToken
    )
    {
        // The vocabulary depends on the process template (Agile, Scrum, Basic all differ), so
        // the state is sent and the vendor's refusal is surfaced. Picking a vocabulary here
        // would be right for one template and silently wrong for the others (design D3).
        return await Patch(
            coordinates,
            vendorStoryId,
            token,
            [Replace("/fields/System.State", state)],
            cancellationToken
        );
    }

    public async Task<ErrorOr<LinkedChange?>> FindLinkedChange(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        string token,
        CancellationToken cancellationToken
    )
    {
        var client = clientFactory.Create(coordinates.Owner, token);

        return await Guarded<LinkedChange?>(
            coordinates,
            async () =>
            {
                // Pull requests appear as artifact relations on the work item.
                var response = await client.GetAsync(
                    $"_apis/wit/workitems/{vendorStoryId}?$expand=relations&api-version={ApiVersion}",
                    cancellationToken
                );

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return (ErrorOr<LinkedChange?>)(LinkedChange?)null;
                }

                if (Translate(response, coordinates) is { } failure)
                {
                    return failure;
                }

                using var document = JsonDocument.Parse(
                    await response.Content.ReadAsStringAsync(cancellationToken)
                );

                var url = document.RootElement.TryGetProperty("relations", out var relations)
                    is true
                    ? relations
                        .EnumerateArray()
                        .Select(relation =>
                            relation.TryGetProperty("url", out var value) ? value.GetString() : null
                        )
                        .FirstOrDefault(value =>
                            value?.Contains("PullRequestId", StringComparison.OrdinalIgnoreCase)
                                is true
                        )
                    : null;

                var number = PullRequestNumber(url);

                return number is null
                    ? (ErrorOr<LinkedChange?>)(LinkedChange?)null
                    : new LinkedChange(number.Value, $"Pull request {number}", url!, "main");
            }
        );
    }

    public async Task<ErrorOr<IReadOnlyList<ChangedFile>>> ListChangeFiles(
        BacklogCoordinates coordinates,
        int changeNumber,
        string token,
        CancellationToken cancellationToken
    )
    {
        // Azure DevOps returns a pull request's changes per iteration, and does not include a
        // unified patch — so every file reports its status with the patch omitted rather than
        // pretending to a diff we do not have.
        var client = clientFactory.Create(coordinates.Owner, token);

        return await Guarded<IReadOnlyList<ChangedFile>>(
            coordinates,
            async () =>
            {
                var response = await client.GetAsync(
                    $"{Uri.EscapeDataString(coordinates.Repository)}/_apis/git/pullrequests/"
                        + $"{changeNumber}/iterations/1/changes?api-version={ApiVersion}",
                    cancellationToken
                );

                if (Translate(response, coordinates) is { } failure)
                {
                    return failure;
                }

                using var document = JsonDocument.Parse(
                    await response.Content.ReadAsStringAsync(cancellationToken)
                );

                if (!document.RootElement.TryGetProperty("changeEntries", out var entries))
                {
                    return ErrorOrFactory.From<IReadOnlyList<ChangedFile>>(new List<ChangedFile>());
                }

                IReadOnlyList<ChangedFile> files =
                [
                    .. entries
                        .EnumerateArray()
                        .Select(entry => new ChangedFile(
                            entry.TryGetProperty("item", out var item)
                            && item.TryGetProperty("path", out var path)
                                ? path.GetString() ?? string.Empty
                                : string.Empty,
                            entry.TryGetProperty("changeType", out var type)
                                ? type.GetString() ?? "modified"
                                : "modified",
                            0,
                            0,
                            null,
                            PatchOmission.TooLarge
                        ))
                        .Where(file => file.Path.Length > 0),
                ];

                return ErrorOrFactory.From<IReadOnlyList<ChangedFile>>(files);
            }
        );
    }

    /// <summary>
    /// Work item comments via the comments API. UNEXERCISED like the rest of this class
    /// (ADR-0005): the endpoint and shape follow the documented contract, verified against no
    /// real organisation.
    /// </summary>
    public async Task<ErrorOr<IReadOnlyList<StoryComment>>> ReadComments(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        DateTimeOffset since,
        string token,
        CancellationToken cancellationToken
    )
    {
        var client = clientFactory.Create(coordinates.Owner, token);

        return await Guarded<IReadOnlyList<StoryComment>>(
            coordinates,
            async () =>
            {
                var response = await client.GetAsync(
                    $"{Uri.EscapeDataString(coordinates.Repository)}/_apis/wit/workItems/{vendorStoryId}/comments?api-version=7.1-preview.4",
                    cancellationToken
                );

                if (Translate(response, coordinates) is { } failure)
                {
                    return failure;
                }

                using var document = JsonDocument.Parse(
                    await response.Content.ReadAsStringAsync(cancellationToken)
                );

                if (!document.RootElement.TryGetProperty("comments", out var comments))
                {
                    return ErrorOrFactory.From<IReadOnlyList<StoryComment>>(
                        Array.Empty<StoryComment>()
                    );
                }

                // The comments API has no `since` parameter, so the filtering that GitHub's
                // vendor does for us happens here instead.
                return ErrorOrFactory.From<IReadOnlyList<StoryComment>>([
                    .. comments
                        .EnumerateArray()
                        .Select(comment => new StoryComment(
                            comment.TryGetProperty("text", out var text)
                                ? text.GetString() ?? string.Empty
                                : string.Empty,
                            comment.TryGetProperty("createdDate", out var created)
                                ? created.GetDateTimeOffset()
                                : DateTimeOffset.MinValue
                        ))
                        .Where(comment => comment.CreatedAt >= since)
                        .OrderBy(comment => comment.CreatedAt),
                ]);
            }
        );
    }

    public async Task<ErrorOr<string>> ReadDocument(
        BacklogCoordinates coordinates,
        string path,
        string reference,
        string token,
        CancellationToken cancellationToken
    )
    {
        var client = clientFactory.Create(coordinates.Owner, token);

        return await Guarded<string>(
            coordinates,
            async () =>
            {
                var response = await client.GetAsync(
                    $"{Uri.EscapeDataString(coordinates.Repository)}/_apis/git/repositories/"
                        + $"{Uri.EscapeDataString(coordinates.Repository)}/items"
                        + $"?path={Uri.EscapeDataString(path)}"
                        + $"&versionDescriptor.version={Uri.EscapeDataString(reference)}"
                        + $"&includeContent=true&api-version={ApiVersion}",
                    cancellationToken
                );

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return (ErrorOr<string>)BacklogErrors.DocumentNotFound(path);
                }

                if (Translate(response, coordinates) is { } failure)
                {
                    return failure;
                }

                using var document = JsonDocument.Parse(
                    await response.Content.ReadAsStringAsync(cancellationToken)
                );

                return document.RootElement.TryGetProperty("content", out var content)
                    ? content.GetString() ?? string.Empty
                    : (ErrorOr<string>)BacklogErrors.DocumentNotFound(path);
            }
        );
    }

    /// <summary>
    /// Stated hypothesis, like every REST call in this class (ADR-0005): the Items API with a
    /// one-level scope path lists a folder's entries; a 404 on the scope path is an absent
    /// directory, the seam's null. Unexercised against a real organisation.
    /// </summary>
    public async Task<ErrorOr<IReadOnlyList<string>?>> ListDirectoryFiles(
        BacklogCoordinates coordinates,
        string path,
        string token,
        CancellationToken cancellationToken
    )
    {
        var client = clientFactory.Create(coordinates.Owner, token);

        return await Guarded<IReadOnlyList<string>?>(
            coordinates,
            async () =>
            {
                var response = await client.GetAsync(
                    $"{Uri.EscapeDataString(coordinates.Repository)}/_apis/git/repositories/"
                        + $"{Uri.EscapeDataString(coordinates.Repository)}/items"
                        + $"?scopePath={Uri.EscapeDataString($"/{path}")}"
                        + $"&recursionLevel=OneLevel&api-version={ApiVersion}",
                    cancellationToken
                );

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    IReadOnlyList<string>? absent = null;
                    return ErrorOrFactory.From(absent);
                }

                if (Translate(response, coordinates) is { } failure)
                {
                    return failure;
                }

                using var document = JsonDocument.Parse(
                    await response.Content.ReadAsStringAsync(cancellationToken)
                );

                return ErrorOrFactory.From<IReadOnlyList<string>?>(
                    ParseDirectoryFileNames(document.RootElement)
                );
            }
        );
    }

    /// <summary>
    /// The Items response's files as names relative to the listed directory — the API answers
    /// repository-absolute paths and marks folders, and nothing outside here learns either (D4).
    /// </summary>
    public static IReadOnlyList<string> ParseDirectoryFileNames(JsonElement root)
    {
        if (!root.TryGetProperty("value", out var entries))
        {
            return [];
        }

        var names = new List<string>();
        foreach (var entry in entries.EnumerateArray())
        {
            var isFolder = entry.TryGetProperty("isFolder", out var folder) && folder.GetBoolean();
            if (isFolder)
            {
                continue;
            }

            if (entry.TryGetProperty("path", out var entryPath))
            {
                var value = entryPath.GetString();
                if (!string.IsNullOrEmpty(value))
                {
                    names.Add(value[(value.LastIndexOf('/') + 1)..]);
                }
            }
        }

        return names;
    }

    /// <summary>Tags are one semicolon-delimited string, which nothing outside here learns (D4).</summary>
    public static IReadOnlyList<string> ParseTags(string? tags) =>
        string.IsNullOrWhiteSpace(tags)
            ? []
            :
            [
                .. tags.Split(
                    ';',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                ),
            ];

    public static string JoinTags(IEnumerable<string> tags) => string.Join("; ", tags);

    /// <summary>The work item as the product's vocabulary — the whole point of the seam.</summary>
    public static VendorStory ToStory(JsonElement workItem)
    {
        var fields = workItem.GetProperty("fields");

        return new VendorStory(
            workItem.GetProperty("id").GetInt32().ToString(CultureInfo.InvariantCulture),
            Field(fields, "System.Title") ?? string.Empty,
            Field(fields, "System.State") ?? string.Empty,
            ParseTags(Field(fields, "System.Tags")),
            Field(fields, "System.Description")
        );
    }

    static string? Field(JsonElement fields, string name) =>
        fields.TryGetProperty(name, out var value) ? value.GetString() : null;

    static int? PullRequestNumber(string? artifactUrl)
    {
        if (artifactUrl is null)
        {
            return null;
        }

        var trailing = artifactUrl.Split('%', '/').LastOrDefault();
        return int.TryParse(trailing, CultureInfo.InvariantCulture, out var number) ? number : null;
    }

    async Task<ErrorOr<Success>> WithTags(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        string token,
        Func<IEnumerable<string>, IEnumerable<string>> change,
        CancellationToken cancellationToken
    )
    {
        // Tags are a whole-field replace, so the current value has to be read first — the
        // vendor's, not the Mirror's, for the same reason the estimate replace does (BR-008).
        var story = await FetchStory(coordinates, vendorStoryId, token, cancellationToken);
        if (story.IsError)
        {
            return story.Errors;
        }

        if (story.Value is null)
        {
            return BacklogErrors.StoryNotFound(vendorStoryId);
        }

        var updated = change(story.Value.Labels).Distinct(StringComparer.OrdinalIgnoreCase);

        return await Patch(
            coordinates,
            vendorStoryId,
            token,
            [Replace("/fields/System.Tags", JoinTags(updated))],
            cancellationToken
        );
    }

    async Task<ErrorOr<Success>> Patch(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        string token,
        object[] operations,
        CancellationToken cancellationToken
    )
    {
        var client = clientFactory.Create(coordinates.Owner, token);

        return await Guarded<Success>(
            coordinates,
            async () =>
            {
                using var content = new StringContent(
                    JsonSerializer.Serialize(operations),
                    Encoding.UTF8,
                    "application/json-patch+json"
                );

                var response = await client.PatchAsync(
                    $"_apis/wit/workitems/{vendorStoryId}?api-version={ApiVersion}",
                    content,
                    cancellationToken
                );

                return Translate(response, coordinates) is { } failure
                    ? failure
                    : (ErrorOr<Success>)Result.Success;
            }
        );
    }

    static object Replace(string path, string value) =>
        new
        {
            op = "add",
            path,
            value,
        };

    static async Task<List<int>> ReadIds(
        HttpResponseMessage response,
        CancellationToken cancellationToken
    )
    {
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken)
        );

        return document.RootElement.TryGetProperty("workItems", out var items)
            ? [.. items.EnumerateArray().Select(item => item.GetProperty("id").GetInt32())]
            : [];
    }

    /// <summary>
    /// Maps HTTP onto the module's closed error set, keeping "wrong project" and "wrong
    /// credential" apart exactly as the GitHub connector does — the taxonomy is the seam's,
    /// not a vendor's.
    /// </summary>
    public static Error? Translate(HttpResponseMessage response, BacklogCoordinates coordinates) =>
        response.StatusCode switch
        {
            HttpStatusCode.OK or HttpStatusCode.Created or HttpStatusCode.NoContent => null,
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                BacklogErrors.CredentialRejected("(supplied credential)"),
            HttpStatusCode.NotFound => BacklogErrors.RepositoryNotFound(
                coordinates.Owner,
                coordinates.Repository
            ),
            HttpStatusCode.TooManyRequests => BacklogErrors.VendorUnavailable(
                "the API rate limit was exceeded"
            ),
            var status => BacklogErrors.VendorUnavailable($"the API returned {(int)status}"),
        };

    static async Task<ErrorOr<T>> Guarded<T>(
        BacklogCoordinates coordinates,
        Func<Task<ErrorOr<T>>> act
    )
    {
        try
        {
            return await act();
        }
        catch (HttpRequestException exception)
        {
            return BacklogErrors.VendorUnavailable(exception.Message);
        }
        catch (JsonException exception)
        {
            return BacklogErrors.VendorUnavailable($"unreadable response: {exception.Message}");
        }
    }
}

/// <summary>
/// Creates a per-token client for one organisation. A seam of its own, exactly as the GitHub
/// factory is, so the connector can be unit-tested without HTTP.
/// </summary>
interface IAzureDevOpsClientFactory
{
    HttpClient Create(string organisation, string token);
}

sealed class AzureDevOpsClientFactory(IHttpClientFactory clients) : IAzureDevOpsClientFactory
{
    public HttpClient Create(string organisation, string token)
    {
        var client = clients.CreateClient(nameof(AzureDevOpsBacklogConnector));
        client.BaseAddress = new Uri($"https://dev.azure.com/{organisation}/");

        // Azure DevOps takes a PAT as basic auth with an empty username.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($":{token}"))
        );

        return client;
    }
}
