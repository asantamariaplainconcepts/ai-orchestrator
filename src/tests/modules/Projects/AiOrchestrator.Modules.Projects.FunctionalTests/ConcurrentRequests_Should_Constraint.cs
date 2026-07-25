using System.Net;
using Shouldly;

namespace AiOrchestrator.Modules.Projects.FunctionalTests;

/// <summary>
/// Pins the Sender-scoping regression: a singleton Sender resolved scoped handlers from the
/// root provider, giving concurrent requests one shared DbContext — an intermittent 500 only
/// the E2E lane's parallel traffic could surface. Sequential suites cannot see this class of
/// bug, so this test makes the concurrency explicit.
/// </summary>
[Collection(ProjectsCollection.Name)]
public class ConcurrentRequests_Should_Constraint(ProjectsApiFixture fixture)
{
    [Fact]
    public async Task ParallelReads_Should_AllSucceed()
    {
        var client = fixture.CreateClient();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 16).Select(_ => client.GetAsync("/api/projects"))
        );

        responses.ShouldAllBe(response => response.StatusCode == HttpStatusCode.OK);
    }
}
