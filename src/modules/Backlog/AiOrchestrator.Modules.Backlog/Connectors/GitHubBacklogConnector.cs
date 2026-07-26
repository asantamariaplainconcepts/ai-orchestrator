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

    public async Task<ErrorOr<Success>> VerifyAccess(
        BacklogCoordinates coordinates,
        string token,
        CancellationToken cancellationToken
    )
    {
        var client = clientFactory.Create(token);

        try
        {
            await client.Repository.Get(coordinates.Owner, coordinates.Repository);
            return Result.Success;
        }
        catch (Exception exception)
        {
            return Translate(exception, coordinates);
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
                    [.. issue.Labels.Select(label => label.Name)]
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
    static Error Translate(Exception exception, BacklogCoordinates coordinates) =>
        exception switch
        {
            AuthorizationException => BacklogErrors.CredentialRejected("(supplied credential)"),
            NotFoundException => BacklogErrors.RepositoryNotFound(
                coordinates.Owner,
                coordinates.Repository
            ),
            RateLimitExceededException => BacklogErrors.VendorUnavailable(
                "the API rate limit was exceeded"
            ),
            ApiException api => BacklogErrors.VendorUnavailable(
                $"the API returned {api.StatusCode}"
            ),
            _ => BacklogErrors.VendorUnavailable(exception.Message),
        };
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
