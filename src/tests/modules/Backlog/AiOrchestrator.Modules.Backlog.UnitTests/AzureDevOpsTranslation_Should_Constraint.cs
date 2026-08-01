using System.Net;
using System.Text.Json;
using AiOrchestrator.Modules.Backlog.Connectors;
using Shouldly;

namespace AiOrchestrator.Modules.Backlog.UnitTests;

/// <summary>
/// What can honestly be verified about the Azure DevOps connector without an organisation: the
/// translation between its model and the product's. The REST calls themselves are a stated
/// hypothesis (ADR-0005) — these tests deliberately do not pretend otherwise.
/// </summary>
public class AzureDevOpsTranslation_Should_Constraint
{
    [Fact]
    public void Tags_Should_RoundTripThroughTheVendorsDelimitedString()
    {
        // System.Tags is one semicolon-delimited string; nothing outside the connector learns
        // that, which is the containment the seam depends on.
        var parsed = AzureDevOpsBacklogConnector.ParseTags("ai:implement; bug ;  ui ");

        parsed.ShouldBe(["ai:implement", "bug", "ui"]);
        AzureDevOpsBacklogConnector
            .ParseTags(AzureDevOpsBacklogConnector.JoinTags(parsed))
            .ShouldBe(parsed);
    }

    [Fact]
    public void NoTags_Should_BeAnEmptyListNotAListWithAnEmptyString()
    {
        AzureDevOpsBacklogConnector.ParseTags(null).ShouldBeEmpty();
        AzureDevOpsBacklogConnector.ParseTags("").ShouldBeEmpty();
        AzureDevOpsBacklogConnector.ParseTags("   ").ShouldBeEmpty();
    }

    [Fact]
    public void AWorkItem_Should_BecomeTheProductsOwnStory()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "id": 42,
              "fields": {
                "System.Title": "Add login",
                "System.State": "Active",
                "System.Tags": "ai:implement; bug",
                "System.Description": "<p>The requirement.</p>"
              }
            }
            """
        );

        var story = AzureDevOpsBacklogConnector.ToStory(document.RootElement);

        story.VendorId.ShouldBe("42");
        story.Title.ShouldBe("Add login");
        // The vendor's own state value, not a normalised one — the same choice GitHub's
        // connector makes, and the reason OPN-003 could close without inventing a vocabulary.
        story.State.ShouldBe("Active");
        story.Labels.ShouldBe(["ai:implement", "bug"]);
        story.Body.ShouldBe("<p>The requirement.</p>");
    }

    [Fact]
    public void AWorkItemMissingOptionalFields_Should_NotThrow()
    {
        using var document = JsonDocument.Parse("""{"id": 7, "fields": {}}""");

        var story = AzureDevOpsBacklogConnector.ToStory(document.RootElement);

        story.VendorId.ShouldBe("7");
        story.Labels.ShouldBeEmpty();
        story.Body.ShouldBeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "CredentialRejected")]
    [InlineData(HttpStatusCode.Forbidden, "CredentialRejected")]
    [InlineData(HttpStatusCode.NotFound, "RepositoryNotFound")]
    [InlineData(HttpStatusCode.TooManyRequests, "VendorUnavailable")]
    [InlineData(HttpStatusCode.InternalServerError, "VendorUnavailable")]
    public void Failures_Should_MapOntoTheSeamsClosedErrorSet(
        HttpStatusCode status,
        string expectedCode
    )
    {
        // The taxonomy belongs to the seam, not to a vendor: "wrong project" and "wrong
        // credential" stay apart here exactly as they do for GitHub, because they have
        // different fixes.
        using var response = new HttpResponseMessage(status);

        var error = AzureDevOpsBacklogConnector.Translate(
            response,
            new BacklogCoordinates("acme", "portal")
        );

        error.ShouldNotBeNull();
        error.Value.Code.ShouldContain(expectedCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Created)]
    [InlineData(HttpStatusCode.NoContent)]
    public void Success_Should_TranslateToNoError(HttpStatusCode status)
    {
        using var response = new HttpResponseMessage(status);

        AzureDevOpsBacklogConnector
            .Translate(response, new BacklogCoordinates("acme", "portal"))
            .ShouldBeNull();
    }

    [Fact]
    public void ADirectoryListing_Should_BecomeNamesRelativeToTheDirectory()
    {
        // The Items API answers repository-absolute paths and marks folders; the seam speaks
        // file names only (#215) — folders skipped, paths cut to their last segment.
        using var document = JsonDocument.Parse(
            """
            {
              "value": [
                { "path": "/ai/prompts", "isFolder": true },
                { "path": "/ai/prompts/estimate.md" },
                { "path": "/ai/prompts/triage.md", "isFolder": false }
              ]
            }
            """
        );

        AzureDevOpsBacklogConnector
            .ParseDirectoryFileNames(document.RootElement)
            .ShouldBe(["estimate.md", "triage.md"]);
    }

    [Fact]
    public void AListingWithNoValue_Should_BeEmptyNotAThrow()
    {
        using var document = JsonDocument.Parse("""{"count": 0}""");

        AzureDevOpsBacklogConnector.ParseDirectoryFileNames(document.RootElement).ShouldBeEmpty();
    }

    [Fact]
    public void TheEstimateFields_Should_CoverTheProcessTemplatesThatHaveOne()
    {
        // Agile and Scrum name it differently and Basic has none — which is why the connector
        // tries in order and refuses rather than assuming (design D3).
        AzureDevOpsBacklogConnector.EstimateFields.ShouldContain(
            "Microsoft.VSTS.Scheduling.StoryPoints"
        );
        AzureDevOpsBacklogConnector.EstimateFields.ShouldContain(
            "Microsoft.VSTS.Scheduling.Effort"
        );
    }
}
