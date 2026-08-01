using AiOrchestrator.Modules.Backlog.Domain;
using AiOrchestrator.Modules.Backlog.Features.Backlog;
using ErrorOr;
using Octokit;

namespace AiOrchestrator.Modules.Backlog.Connectors;

/// <summary>
/// GitHub implementation of the seam. Octokit is confined to this file: nothing it returns leaves
/// here except as the product's own types (design D4).
/// </summary>
sealed class GitHubBacklogConnector(IGitHubClientFactory clientFactory) : IBacklogConnector
{
    public BacklogVendor Vendor => BacklogVendor.GitHub;

    /// <summary>
    /// GitHub requires a colour when creating a label and has no "let the vendor decide".
    /// One neutral grey for every label this product creates: picking per-action colours would
    /// be this repository deciding how somebody else's backlog should look.
    /// </summary>
    internal const string DefaultLabelColour = "ededed";

    public async Task<CredentialVerdict> VerifyAccess(
        BacklogCoordinates coordinates,
        string documentPath,
        string token,
        CancellationToken cancellationToken
    )
    {
        var client = clientFactory.Create(token);

        // The reads this product performs, made early (design D1). Repository.Get used to stand in
        // for all of them and proved only that the coordinates resolve — it succeeds on the
        // metadata permission every fine-grained token is created with, which is how a credential
        // refused the repository's contents was stored as verified.
        var stories = await Probe(
            Capabilities.Stories,
            coordinates,
            () =>
                client.Issue.GetAllForRepository(
                    coordinates.Owner,
                    coordinates.Repository,
                    new RepositoryIssueRequest { State = ItemStateFilter.Open },
                    new ApiOptions { PageSize = 1, PageCount = 1 }
                )
        );

        var documents = await Probe(
            Capabilities.Documents,
            coordinates,
            () =>
                client.Repository.Content.GetAllContentsByRef(
                    coordinates.Owner,
                    coordinates.Repository,
                    documentPath,
                    "HEAD"
                ),
            // Absence is not refusal (design D6): a path that does not exist says this repository
            // has not adopted the framework's layout, not that we may not look. Refusing those
            // would refuse almost every repository this product is pointed at.
            absenceIsSuccess: true
        );

        return CredentialVerdict.Of(stories, documents);
    }

    /// <summary>
    /// One capability, attempted for real. Nothing is inferred from declared scopes — GitHub
    /// exposes those unevenly between classic and fine-grained tokens, so a check built on them
    /// would be reliable for one kind of credential and misleading for the other.
    /// </summary>
    async Task<CapabilityResult> Probe<T>(
        string capability,
        BacklogCoordinates coordinates,
        Func<Task<T>> call,
        bool absenceIsSuccess = false
    )
    {
        try
        {
            await call();
            return CapabilityResult.Passed(capability);
        }
        catch (NotFoundException) when (absenceIsSuccess)
        {
            return CapabilityResult.Passed(capability);
        }
        catch (Exception exception)
        {
            return CapabilityResult.Refused(
                capability,
                Translate(exception, coordinates, capability)
            );
        }
    }

    public async Task<ErrorOr<BacklogSnapshot>> FetchStories(
        BacklogCoordinates coordinates,
        string token,
        CancellationToken cancellationToken
    )
    {
        var client = clientFactory.Create(token);

        try
        {
            // Octokit paginates for us; ItemStateFilter.Open keeps this to the working backlog.
            var request = new RepositoryIssueRequest { State = ItemStateFilter.Open };
            var issues = await client.Issue.GetAllForRepository(
                coordinates.Owner,
                coordinates.Repository,
                request
            );

            var stories = issues
                // GitHub models pull requests as issues; they are not backlog Stories.
                .Where(issue => issue.PullRequest is null)
                .Select(issue => new VendorStory(
                    issue.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    issue.Title,
                    // The vendor's own state value, deliberately not normalised (design D9).
                    issue.State.StringValue,
                    [.. issue.Labels.Select(label => label.Name)],
                    issue.Body
                ))
                .ToList();

            return new BacklogSnapshot(stories);
        }
        catch (Exception exception)
        {
            return Translate(exception, coordinates);
        }
    }

    public async Task<ErrorOr<Success>> ApplyLabel(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        string label,
        string token,
        CancellationToken cancellationToken
    )
    {
        if (!TryParseIssueNumber(vendorStoryId, out var number))
        {
            return BacklogErrors.StoryNotFound(vendorStoryId);
        }

        var client = clientFactory.Create(token);

        try
        {
            // Add-to-set at the vendor: applying a label the issue already carries is a no-op
            // by GitHub's own semantics (design D3).
            await client.Issue.Labels.AddToIssue(
                coordinates.Owner,
                coordinates.Repository,
                number,
                [label]
            );
            return Result.Success;
        }
        catch (NotFoundException)
        {
            // For an *apply*, 404 means the issue (or repository) is gone — a real refusal.
            return BacklogErrors.StoryNotFound(vendorStoryId);
        }
        catch (Exception exception)
        {
            return Translate(exception, coordinates);
        }
    }

    public async Task<ErrorOr<Success>> EnsureLabel(
        BacklogCoordinates coordinates,
        string label,
        string token,
        CancellationToken cancellationToken
    )
    {
        var client = clientFactory.Create(token);

        try
        {
            await client.Issue.Labels.Get(coordinates.Owner, coordinates.Repository, label);
            // Already there. Deliberately not compared on colour or description: this method
            // guarantees the label is choosable, not that it looks a particular way, and
            // rewriting an Admin's colour would be a surprise nobody asked for.
            return Result.Success;
        }
        catch (NotFoundException)
        {
            // Falls through to creation — a missing label is the reason to call this, not a
            // failure. A missing *repository* raises the same exception and is caught below by
            // the create call, where 404 genuinely means the repository.
        }
        catch (Exception exception)
        {
            return Translate(exception, coordinates);
        }

        try
        {
            await client.Issue.Labels.Create(
                coordinates.Owner,
                coordinates.Repository,
                new NewLabel(label, DefaultLabelColour)
            );
            return Result.Success;
        }
        catch (NotFoundException)
        {
            return BacklogErrors.RepositoryNotFound(coordinates.Owner, coordinates.Repository);
        }
        catch (ApiValidationException)
        {
            // 422 here means the label was created between the Get and the Create — two Admins
            // pressing the button at once. The postcondition holds, so this is success.
            return Result.Success;
        }
        catch (Exception exception)
        {
            return Translate(exception, coordinates);
        }
    }

    public async Task<ErrorOr<Success>> RemoveLabel(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        string label,
        string token,
        CancellationToken cancellationToken
    )
    {
        if (!TryParseIssueNumber(vendorStoryId, out var number))
        {
            return BacklogErrors.StoryNotFound(vendorStoryId);
        }

        var client = clientFactory.Create(token);

        try
        {
            await client.Issue.Labels.RemoveFromIssue(
                coordinates.Owner,
                coordinates.Repository,
                number,
                label
            );
            return Result.Success;
        }
        catch (NotFoundException)
        {
            // GitHub answers 404 both for "label not on the issue" and "issue gone". Either
            // way the desired end state — the story does not carry the label — holds, so a
            // remove treats 404 as the idempotent no-op (design D3).
            return Result.Success;
        }
        catch (Exception exception)
        {
            return Translate(exception, coordinates);
        }
    }

    public async Task<ErrorOr<LinkedChange?>> FindLinkedChange(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        string token,
        CancellationToken cancellationToken
    )
    {
        if (!TryParseIssueNumber(vendorStoryId, out var number))
        {
            return BacklogErrors.StoryNotFound(vendorStoryId);
        }

        var client = clientFactory.Create(token);

        try
        {
            // The timeline carries cross-reference events: a PR whose body says "Closes #41"
            // appears here. Newest first, because the current change is the interesting one.
            var timeline = await client.Issue.Timeline.GetAllForIssue(
                coordinates.Owner,
                coordinates.Repository,
                number
            );

            var referencing = timeline
                .Where(entry => entry.Source?.Issue?.PullRequest is not null)
                .Select(entry => entry.Source!.Issue!)
                .OrderByDescending(issue => issue.Number)
                .FirstOrDefault();

            if (referencing is null)
            {
                return (LinkedChange?)null;
            }

            var pullRequest = await client.PullRequest.Get(
                coordinates.Owner,
                coordinates.Repository,
                referencing.Number
            );

            return new LinkedChange(
                pullRequest.Number,
                pullRequest.Title,
                pullRequest.HtmlUrl,
                // The head SHA, not the branch name: the ref documents are read at must mean
                // one thing even while the branch keeps moving during a read.
                pullRequest.Head.Sha
            );
        }
        catch (Exception exception)
        {
            return Translate(exception, coordinates);
        }
    }

    /// <summary>The vendor omits patches for binary files; large ones we omit ourselves.</summary>
    const int PatchSizeLimit = 200_000;

    public async Task<ErrorOr<IReadOnlyList<ChangedFile>>> ListChangeFiles(
        BacklogCoordinates coordinates,
        int changeNumber,
        string token,
        CancellationToken cancellationToken
    )
    {
        var client = clientFactory.Create(token);

        try
        {
            var files = await client.PullRequest.Files(
                coordinates.Owner,
                coordinates.Repository,
                changeNumber
            );

            return files
                .Select(file => new ChangedFile(
                    file.FileName,
                    file.Status,
                    file.Additions,
                    file.Deletions,
                    // A patch we cannot show is stated, never truncated (design D3).
                    string.IsNullOrEmpty(file.Patch)
                    || file.Patch.Length > PatchSizeLimit
                        ? null
                        : file.Patch,
                    string.IsNullOrEmpty(file.Patch) ? PatchOmission.Binary
                        : file.Patch.Length > PatchSizeLimit ? PatchOmission.TooLarge
                        : null
                ))
                .OrderBy(file => file.Path, StringComparer.Ordinal)
                .ToList();
        }
        catch (Exception exception)
        {
            return Translate(exception, coordinates);
        }
    }

    public async Task<ErrorOr<IReadOnlyList<StoryComment>>> ReadComments(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        DateTimeOffset since,
        string token,
        CancellationToken cancellationToken
    )
    {
        if (!TryParseIssueNumber(vendorStoryId, out var number))
        {
            return BacklogErrors.StoryNotFound(vendorStoryId);
        }

        var client = clientFactory.Create(token);

        try
        {
            // Octokit passes `since` to the API, so the vendor does the filtering; this is one
            // page for any Run that is actually mid-conversation.
            var comments = await client.Issue.Comment.GetAllForIssue(
                coordinates.Owner,
                coordinates.Repository,
                number,
                new IssueCommentRequest { Since = since }
            );

            return ErrorOrFactory.From<IReadOnlyList<StoryComment>>([
                .. comments
                    .Where(comment => comment.CreatedAt >= since)
                    .OrderBy(comment => comment.CreatedAt)
                    .Select(comment => new StoryComment(
                        comment.Body ?? string.Empty,
                        comment.CreatedAt
                    )),
            ]);
        }
        catch (NotFoundException)
        {
            return BacklogErrors.StoryNotFound(vendorStoryId);
        }
        catch (Exception exception)
        {
            return Translate(exception, coordinates);
        }
    }

    public async Task<ErrorOr<string>> ReadDocument(
        BacklogCoordinates coordinates,
        string path,
        string reference,
        string token,
        CancellationToken cancellationToken
    )
    {
        var client = clientFactory.Create(token);

        try
        {
            var contents = await client.Repository.Content.GetAllContentsByRef(
                coordinates.Owner,
                coordinates.Repository,
                path,
                reference
            );

            var content = contents.FirstOrDefault()?.Content;
            return content is null ? BacklogErrors.DocumentNotFound(path) : content;
        }
        catch (NotFoundException)
        {
            return BacklogErrors.DocumentNotFound(path);
        }
        catch (Exception exception)
        {
            return Translate(exception, coordinates);
        }
    }

    public async Task<ErrorOr<IReadOnlyList<string>?>> ListDirectoryFiles(
        BacklogCoordinates coordinates,
        string path,
        string token,
        CancellationToken cancellationToken
    )
    {
        var client = clientFactory.Create(token);

        try
        {
            // No ref: the contents API answers from the default branch, which is exactly the
            // branch the picker promises to read (#215).
            var contents = await client.Repository.Content.GetAllContents(
                coordinates.Owner,
                coordinates.Repository,
                path
            );

            return ErrorOrFactory.From<IReadOnlyList<string>?>([
                .. contents
                    .Where(entry => entry.Type == ContentType.File)
                    .Select(entry => entry.Name),
            ]);
        }
        catch (NotFoundException)
        {
            // An absent directory is an ordinary outcome, not a refusal — the seam's null.
            IReadOnlyList<string>? absent = null;
            return ErrorOrFactory.From(absent);
        }
        catch (Exception exception)
        {
            return Translate(exception, coordinates);
        }
    }

    public async Task<ErrorOr<VendorStory?>> FetchStory(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        string token,
        CancellationToken cancellationToken
    )
    {
        if (!TryParseIssueNumber(vendorStoryId, out var number))
        {
            return (VendorStory?)null;
        }

        try
        {
            var issue = await clientFactory
                .Create(token)
                .Issue.Get(coordinates.Owner, coordinates.Repository, number);

            return new VendorStory(
                issue.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                issue.Title,
                issue.State.StringValue,
                [.. issue.Labels.Select(label => label.Name)],
                issue.Body
            );
        }
        catch (NotFoundException)
        {
            return (VendorStory?)null;
        }
        catch (Exception exception)
        {
            return Translate(exception, coordinates);
        }
    }

    public async Task<ErrorOr<Success>> AddComment(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        string comment,
        string token,
        CancellationToken cancellationToken
    )
    {
        if (!TryParseIssueNumber(vendorStoryId, out var number))
        {
            return BacklogErrors.StoryNotFound(vendorStoryId);
        }

        try
        {
            await clientFactory
                .Create(token)
                .Issue.Comment.Create(coordinates.Owner, coordinates.Repository, number, comment);
            return Result.Success;
        }
        catch (NotFoundException)
        {
            return BacklogErrors.StoryNotFound(vendorStoryId);
        }
        catch (Exception exception)
        {
            return Translate(exception, coordinates);
        }
    }

    /// <summary>GitHub's whole state vocabulary for an issue — anything else is a refusal.</summary>
    static readonly Dictionary<string, ItemState> States = new(StringComparer.OrdinalIgnoreCase)
    {
        ["open"] = ItemState.Open,
        ["closed"] = ItemState.Closed,
    };

    public async Task<ErrorOr<Success>> SetState(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        string state,
        string token,
        CancellationToken cancellationToken
    )
    {
        if (!TryParseIssueNumber(vendorStoryId, out var number))
        {
            return BacklogErrors.StoryNotFound(vendorStoryId);
        }

        if (!States.TryGetValue(state.Trim(), out var target))
        {
            return BacklogErrors.StateNotAccepted(state, string.Join(", ", States.Keys));
        }

        try
        {
            await clientFactory
                .Create(token)
                .Issue.Update(
                    coordinates.Owner,
                    coordinates.Repository,
                    number,
                    new IssueUpdate { State = target }
                );
            return Result.Success;
        }
        catch (NotFoundException)
        {
            return BacklogErrors.StoryNotFound(vendorStoryId);
        }
        catch (Exception exception)
        {
            return Translate(exception, coordinates);
        }
    }

    static bool TryParseIssueNumber(string vendorStoryId, out int number) =>
        int.TryParse(
            vendorStoryId,
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out number
        );

    /// <summary>
    /// Maps vendor failures onto our closed error set, keeping "wrong repository" and "wrong
    /// credential" apart. GitHub answers 404 for a repository the credential cannot see, which is
    /// indistinguishable from one that does not exist — so that case is reported as coordinates,
    /// and the message says both possibilities out loud rather than guessing.
    /// </summary>
    static Error Translate(
        Exception exception,
        BacklogCoordinates coordinates,
        string capability = "this repository"
    ) =>
        exception switch
        {
            AuthorizationException => BacklogErrors.CredentialRejected("(supplied credential)"),
            NotFoundException => BacklogErrors.RepositoryNotFound(
                coordinates.Owner,
                coordinates.Repository
            ),
            // Before the generic ApiException arm, and that ordering is the fix: a rate limit is a
            // 403 too, and it is the one 403 that is not about permissions.
            RateLimitExceededException => BacklogErrors.VendorUnavailable(
                "the API rate limit was exceeded"
            ),
            // A vendor that answered is not a vendor that could not be reached (#132, design D3).
            // Octokit's ForbiddenException lands here, and its message carries GitHub's own
            // sentence — "Resource not accessible by personal access token" — which names the
            // missing permission far better than a status code does.
            ForbiddenException forbidden => BacklogErrors.PermissionRefused(
                capability,
                Reason(forbidden)
            ),
            ApiException api => BacklogErrors.VendorUnavailable(
                $"the API returned {api.StatusCode}"
            ),
            _ => BacklogErrors.VendorUnavailable(exception.Message),
        };

    /// <summary>
    /// The vendor's own words, preferring the API's message over the exception's. An empty one
    /// falls back to saying so rather than to an empty sentence.
    /// </summary>
    static string Reason(ApiException exception)
    {
        var message = exception.ApiError?.Message;
        return string.IsNullOrWhiteSpace(message)
            ? string.IsNullOrWhiteSpace(exception.Message)
                ? "the vendor gave no reason"
                : exception.Message
            : message;
    }
}

/// <summary>
/// Creates a per-token client. A seam of its own so tests can supply a fake without reaching for
/// HTTP, and so credential handling stays in one place.
/// </summary>
interface IGitHubClientFactory
{
    IGitHubClient Create(string token);
}

sealed class GitHubClientFactory(BacklogOptions options) : IGitHubClientFactory
{
    static readonly ProductHeaderValue Product = new("ai-orchestrator");

    public IGitHubClient Create(string token)
    {
        var client = options.GitHubBaseAddress is null
            ? new GitHubClient(Product)
            : new GitHubClient(Product, options.GitHubBaseAddress);

        client.Credentials = new Credentials(token);
        return client;
    }
}
