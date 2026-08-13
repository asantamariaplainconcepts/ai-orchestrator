using System.Diagnostics;
using Microsoft.Playwright;
using Shouldly;

namespace AiOrchestrator.EndToEndTests;

/// <summary>
/// #347 section 6 — an Admin adds a Project by naming a folder, and the form tells them the truth about
/// what the folder yielded.
/// <para>
/// Driven through the browser rather than asserted in a unit test because this repository has **no
/// frontend test framework** — no vitest, no testing-library, no `test` script in
/// `src/frontend/package.json`. This suite is the only lane that exercises the built UI, so it is where
/// "frontend tests for the posture gating and the four named failures" has to live. Note the suite
/// serves the **built** bundle: a `.tsx` edit is invisible here until `pnpm build` has run.
/// </para>
/// <para>
/// The four failures are asserted as four different sentences on purpose. "That folder didn't work"
/// would be true of all of them and useful for none — a path that is not a directory, a directory that
/// is not a repository, a repository with no `origin`, and an `origin` neither vendor recognises have
/// four different fixes, and the failing check is the only thing that says which one to make.
/// </para>
/// </summary>
[Collection(AppHostCollection.Name)]
[Trait("Category", "E2E")]
public class AddProjectByFolder_Should_Constraint(AppHostFixture fixture)
{
    /// <summary>
    /// The dev loop leaves <c>Habitat__LocalFolderUnavailableReason</c> unset, so this habitat offers
    /// the folder. The input's presence is read from the deployment capabilities and never derived in
    /// the browser, which is why this is worth asserting end to end rather than in isolation.
    /// </summary>
    [Fact]
    public async Task AHabitatThatOffersAFolder_Should_ShowTheInputAndWhatItRequires()
    {
        var page = await NewProjectsPage();

        var folder = page.GetByLabel("Folder on this machine (optional)");
        await folder.WaitForAsync(new() { Timeout = 30_000 });

        // The explanation and the honest permission statement travel with the input — a folder field
        // with no account of what reaching the vendor requires would imply the product had checked.
        (await page.GetByText("Its origin names the vendor").IsVisibleAsync()).ShouldBeTrue();
        (
            await page.GetByText("the product cannot verify what it was granted").IsVisibleAsync()
        ).ShouldBeTrue();
    }

    [Fact]
    public async Task APathThatIsNotADirectory_Should_SayThat()
    {
        var file = Path.Combine(Path.GetTempPath(), $"aio-e2e-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(file, "not a directory");

        try
        {
            var page = await Submit(file);
            await ExpectMessage(page, "That path is not a directory on this machine.");
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ADirectoryThatIsNotARepository_Should_SayThat()
    {
        var directory = FreshDirectory();

        try
        {
            var page = await Submit(directory);
            await ExpectMessage(page, "That folder is not a git repository.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ARepositoryWithNoOrigin_Should_SayThat()
    {
        var directory = FreshDirectory();
        Git(directory, "init");

        try
        {
            var page = await Submit(directory);
            await ExpectMessage(page, "That repository has no origin remote to read.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task AnOriginNeitherVendorRecognises_Should_SayThat()
    {
        var directory = FreshDirectory();
        Git(directory, "init");
        Git(directory, "remote add origin https://gitlab.com/someone/something.git");

        try
        {
            var page = await Submit(directory);
            await ExpectMessage(
                page,
                "That origin is neither a GitHub nor an Azure DevOps remote."
            );
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    async Task<IPage> NewProjectsPage()
    {
        var page = await fixture.Browser.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 800);
        await page.GotoAsync($"{fixture.ServerBaseUrl}projects");
        return page;
    }

    /// <summary>Names a Project and a folder, and submits. The page is returned for assertions.</summary>
    async Task<IPage> Submit(string folder)
    {
        var page = await NewProjectsPage();

        var name = page.GetByLabel("Project name");
        await name.WaitForAsync(new() { Timeout = 30_000 });
        await name.FillAsync($"folder-{Guid.NewGuid():N}"[..24]);

        var input = page.GetByLabel("Folder on this machine (optional)");
        await input.WaitForAsync(new() { Timeout = 10_000 });
        await input.FillAsync(folder);

        await page.GetByRole(AriaRole.Button, new() { Name = "Create project" }).ClickAsync();

        return page;
    }

    static async Task ExpectMessage(IPage page, string expected) =>
        await page.GetByText(expected).WaitForAsync(new() { Timeout = 30_000 });

    static string FreshDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"aio-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>
    /// git, run for real in a throwaway directory. The server inspects the folder with the machine's own
    /// git, so the fixture has to produce a folder git actually agrees about — a faked <c>.git</c> would
    /// assert against a shape the product never sees.
    /// </summary>
    static void Git(string directory, string arguments)
    {
        using var process = Process.Start(
            new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = directory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            }
        );

        process.ShouldNotBeNull();
        process.WaitForExit(30_000);
        process.ExitCode.ShouldBe(0, $"git {arguments} failed in {directory}");
    }
}
