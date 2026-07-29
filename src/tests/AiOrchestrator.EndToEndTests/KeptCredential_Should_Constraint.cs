using System.Text.Json;
using Microsoft.Playwright;
using Shouldly;

namespace AiOrchestrator.EndToEndTests;

/// <summary>
/// #160 — the case observed on dev: a Connector exists, the Admin wants to change one setting, and the
/// Token field is empty. Asserted through the portal specifically because the block was a
/// <b>client-side</b> submit guard: the API test cannot see it, since the request was never sent.
/// </summary>
[Collection(AppHostCollection.Name)]
[Trait("Category", "E2E")]
public class KeptCredential_Should_Constraint(AppHostFixture fixture)
{
    [Fact]
    public async Task ASettingChange_Should_SaveWithTheTokenFieldLeftEmpty()
    {
        fixture.GitHub.Repositories.Add("acme/portal");

        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, "Keep the stored credential");

        // Configured by naming the secret this habitat holds, not by pasting: the E2E environment
        // supplies `Secrets__e2e-github` and has no writable store, so the paste path is refused there
        // by design. Either way a Connector exists with a credential worth keeping, which is what #160
        // is about.
        var configured = await page.APIRequest.PutAsync(
            $"{fixture.ServerBaseUrl}api/projects/{projectId}/connector",
            new APIRequestContextOptions
            {
                DataObject = new
                {
                    owner = "acme",
                    repository = "portal",
                    secretName = AppHostFixture.SecretName,
                },
            }
        );
        configured.Status.ShouldBe(200, await configured.TextAsync());

        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}?tab=settings");

        var edit = page.GetByRole(AriaRole.Button, new() { Name = "Edit Connector" });
        await edit.WaitForAsync(new() { Timeout = 30_000 });
        await edit.ClickAsync();

        // Deliberately the paste mode with an empty Token field — the exact state observed on dev, and
        // the only state that exercises the reuse path. In the naming mode the form re-sends the
        // Connector's own secret name, so it would never send "neither" and this test would pass
        // without the change it exists to cover.
        await page.GetByLabel("Access token").SelectOptionAsync("paste");

        // The only thing touched. The Token field stays empty, which is the whole point.
        await page.GetByLabel("Prompts directory").FillAsync("prompts/ours");
        await page.GetByRole(AriaRole.Button, new() { Name = "Configure connector" }).ClickAsync();

        var stored = await Eventually(
            () => PromptDirectory(page, projectId),
            directory => directory == "prompts/ours"
        );
        stored.ShouldBe("prompts/ours");
    }

    static async Task<T> Eventually<T>(Func<Task<T>> read, Func<T, bool> until)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(15);
        var latest = await read();

        while (!until(latest) && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(250);
            latest = await read();
        }

        return latest;
    }

    async Task<string?> PromptDirectory(IPage page, Guid projectId)
    {
        var response = await page.APIRequest.GetAsync(
            $"{fixture.ServerBaseUrl}api/projects/{projectId}/backlog"
        );
        response.Status.ShouldBe(200, await response.TextAsync());

        using var document = JsonDocument.Parse(await response.TextAsync());
        var connector = document.RootElement.GetProperty("connector");
        return connector.ValueKind == JsonValueKind.Null
            ? null
            : connector.GetProperty("promptDirectory").GetString();
    }

    async Task<Guid> CreateProject(IPage page, string name)
    {
        var response = await page.APIRequest.PostAsync(
            $"{fixture.ServerBaseUrl}api/projects",
            new APIRequestContextOptions { DataObject = new { name } }
        );
        response.Status.ShouldBe(201, await response.TextAsync());

        using var document = JsonDocument.Parse(await response.TextAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }
}
