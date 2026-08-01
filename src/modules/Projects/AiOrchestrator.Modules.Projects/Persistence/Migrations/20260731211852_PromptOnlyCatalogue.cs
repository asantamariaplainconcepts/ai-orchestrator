using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Projects.Persistence.Migrations
{
    /// <summary>
    /// The catalogue collapses to one action (#162).
    /// <para>
    /// Two things, and only the first is EF's. The column rename it detected on its own: what was
    /// the grill's <c>RubricPath</c> is the prompt's path now, and every value survives — #150 had
    /// already made that column how a repository prompt names its file, which is why it is renamed
    /// rather than dropped with the action it was named for.
    /// </para>
    /// <para>
    /// The second is the delete below, which EF cannot know about. An Automation naming a retired
    /// action cannot be converted: there is no prompt file to point it at, and inventing a path
    /// would leave an Automation that matches Stories and then fails every Run — worse than one
    /// that is gone. Nothing is in production, which is what makes this safe rather than merely
    /// convenient, and past Runs are unaffected because a Run whose Automation is gone already
    /// renders (#116).
    /// </para>
    /// </summary>
    public partial class PromptOnlyCatalogue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RubricPath",
                schema: "projects",
                table: "automations",
                newName: "PromptPath"
            );

            // Written out rather than "everything except RepositoryPrompt": a list names what is
            // being deleted, so a reader of this migration can see it, and a member added to the
            // enum later cannot be swept away by a filter nobody revisited.
            migrationBuilder.Sql(
                """
                DELETE FROM projects.automations
                WHERE "Action" IN (
                    'ImplementToPullRequest', 'RefineOrComment', 'TransitionState',
                    'Estimate', 'GrillToReady', 'ProposeSpec', 'SyncChange'
                );
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PromptPath",
                schema: "projects",
                table: "automations",
                newName: "RubricPath"
            );

            // The delete has no reverse. The rows are gone and the down migration cannot invent
            // them back; saying so here is better than a comment-free asymmetry somebody has to
            // work out from the absence.
        }
    }
}
