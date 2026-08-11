using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Projects.Persistence.Migrations
{
    /// <summary>
    /// A derived graph of labels becomes a stored lifecycle an Automation claims a transition of
    /// (#310, AC 10).
    /// <para>
    /// <b>Hand-written, for the reason <c>20260730222648_OutputLabelSet.cs:9-19</c> already paid for
    /// once.</b> What EF scaffolds for this change is two <c>AddColumn</c>s — schema-correct, and it
    /// leaves <c>ToStage</c> null on every row and <c>LifecycleStages</c> empty on every project. No
    /// test of the new shape would notice, because the new shape works fine empty: the deployment
    /// would come up with every workflow edge gone and every board a single column. That is the same
    /// silent loss the output-label widening avoided, arriving through omission rather than through a
    /// <c>DropColumn</c>.
    /// </para>
    /// <para>
    /// So the scaffolded pair of columns is only step one, and the rest of <c>Up</c> reads the edges
    /// out of the labels the way the board reads them today and writes them down:
    /// </para>
    /// <list type="number">
    /// <item>add the two columns;</item>
    /// <item>record, per Automation, whether it had a hand-off and how many labels it held — the
    /// evidence the last step checks itself against, captured before anything is rewritten;</item>
    /// <item>derive each claim: <c>ToStage</c> is the <b>first</b> output label matching another
    /// <b>enabled</b> sibling's trigger label in the same project, <b>compared case-insensitively</b>,
    /// and exactly that label leaves <c>OutputLabels</c>. Every other label stays a mark, including
    /// one that matches no sibling trigger;</item>
    /// <item>build each project's stage list by walking those claims in the order the board draws
    /// them today (<c>KanbanBoard.tsx:98-137</c>): roots first, then whatever each hands to, then the
    /// loose ones;</item>
    /// <item>refuse to commit unless every Automation that had a hand-off claims one and no label
    /// went missing — per row, naming the row.</item>
    /// </list>
    /// <para>
    /// <b>Case is folded, not carried</b> (DEC-056). <c>buildChains</c> compares through a plain
    /// <c>Map</c> and <c>planHandoff.ts:16-20</c> records that this disagrees with product identity, so
    /// a case-sensitive read here would drop edges the canvas draws today. Every comparison below is
    /// <c>lower()</c>, matching <c>IX_automations_trigger_identity</c> and the domain's
    /// <c>OrdinalIgnoreCase</c>.
    /// </para>
    /// <para>
    /// <b>Two places AC 10 does not decide, decided here and written down rather than left to be
    /// discovered.</b> First, an Automation with <i>two</i> matching output labels had two edges under
    /// the old model and can claim only one now — AC 13 makes branching unrepresentable, so the second
    /// match is kept as a mark. That is why the guard counts Automations that handed on rather than
    /// (Automation, matching label) pairs: the pair count cannot be preserved by a model that has
    /// nowhere to put the second pair, and asserting it would fail on the first branching row. What
    /// <i>is</i> preserved exactly is every label: after this migration each Automation's label count
    /// equals its marks plus its claim. Second, the stage list holds the labels <b>the board drew</b>,
    /// which means enabled Automations only; a <i>disabled</i> Automation's hand-off is still preserved
    /// as a claim (it is configuration, and AC 10 says each Automation claims), so its from-stage can
    /// be absent from the lifecycle until something saves it — at which point
    /// <c>Project.ClaimTransition</c> inserts the stage, because that is the one place the adjacency
    /// invariant lives (design D4).
    /// </para>
    /// <para>
    /// <b><c>Down</c> is lossy and says so.</b> The claim goes back to the front of
    /// <c>OutputLabels</c>, which is where the old walk read the edge from, so the flow the canvas
    /// derives is the flow that was configured. <b>The order of a project's stages cannot survive
    /// the reverse</b> — the old shape has nowhere to put an order, which is the whole reason ADR-0022
    /// exists — and neither can a stage no Automation's label mentions. Written down rather than
    /// pretended away, exactly as <c>20260730222648_OutputLabelSet.cs:53-80</c> does.
    /// </para>
    /// </summary>
    public partial class ClaimedTransition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "LifecycleStages",
                schema: "projects",
                table: "projects",
                type: "character varying(200)[]",
                nullable: false,
                defaultValue: new string[0]
            );

            migrationBuilder.AddColumn<string>(
                name: "ToStage",
                schema: "projects",
                table: "automations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true
            );

            // Step 2. The before picture, per Automation, taken before anything is rewritten: did it
            // hand on at all, and how many labels did it hold. A temp table rather than two counters,
            // so the guard at the end can name the row it is unhappy about instead of reporting that
            // some total moved. Dropped explicitly rather than ON COMMIT DROP, which would take it
            // away between statements if migrations were ever applied without a transaction.
            migrationBuilder.Sql(
                """
                CREATE TEMP TABLE handoffs_before AS
                SELECT a."Id" AS automation,
                       cardinality(a."OutputLabels") AS labels,
                       EXISTS (
                           SELECT 1
                           FROM unnest(a."OutputLabels") AS label(value)
                           WHERE EXISTS (
                               SELECT 1
                               FROM projects.automations sibling
                               WHERE sibling."ProjectId" = a."ProjectId"
                                 AND sibling."Id" <> a."Id"
                                 AND sibling."Enabled"
                                 AND lower(sibling."TriggerLabel") = lower(label.value)
                           )
                       ) AS handed_on
                FROM projects.automations a;
                """
            );

            // Step 3. The claim. DISTINCT ON with ORDER BY ord is "the first of its output labels",
            // which is the same first the board takes (KanbanBoard.tsx:101-105) now that an output is
            // a set. The stored spelling is the sibling *trigger's*, not the output label's: a claim
            // names a stage of the lifecycle, and Project.ResolveStage:79-90 answers with the stage
            // that exists rather than the spelling somebody typed. Removal is by ordinal, so exactly
            // the matched label leaves and a second spelling of it would not go with it.
            migrationBuilder.Sql(
                """
                WITH claimed AS (
                    SELECT DISTINCT ON (a."Id")
                           a."Id" AS automation,
                           label.ord AS matched_at,
                           sibling."TriggerLabel" AS stage
                    FROM projects.automations a
                    CROSS JOIN unnest(a."OutputLabels") WITH ORDINALITY AS label(value, ord)
                    JOIN projects.automations sibling
                      ON sibling."ProjectId" = a."ProjectId"
                     AND sibling."Id" <> a."Id"
                     AND sibling."Enabled"
                     AND lower(sibling."TriggerLabel") = lower(label.value)
                    ORDER BY a."Id", label.ord, sibling."TriggerLabel", sibling."Id"
                )
                UPDATE projects.automations a
                SET "ToStage" = claimed.stage,
                    "OutputLabels" = COALESCE(
                        (
                            SELECT array_agg(mark.value ORDER BY mark.ord)
                            FROM unnest(a."OutputLabels") WITH ORDINALITY AS mark(value, ord)
                            WHERE mark.ord <> claimed.matched_at
                        ),
                        '{}'
                    )::character varying(200)[]
                FROM claimed
                WHERE a."Id" = claimed.automation;
                """
            );

            // Step 4. The order that was on screen. A faithful port of the board's walk rather than a
            // WITH RECURSIVE one: the walk keeps a *global* placed set across chains
            // (KanbanBoard.tsx:110-127), so where two Automations hand to the same stage the second
            // chain is truncated, and truncation decides whether that chain counts as flow or as
            // loose. A recursive CTE cannot see the earlier chains' placements, and a version that
            // ignored them would order those two projects differently from the board they came from.
            migrationBuilder.Sql(
                """
                DO $walk$
                DECLARE
                    project uuid;
                    root uuid;
                    orphan RECORD;
                    node uuid;
                    handed_to uuid;
                    trigger_label text;
                    chain text[];
                    flow text[];
                    loose text[];
                    placed uuid[];
                    stages text[];
                    stage text;
                BEGIN
                    FOR project IN SELECT "Id" FROM projects.projects ORDER BY "Id" LOOP
                        flow := '{}';
                        loose := '{}';
                        placed := '{}';

                        -- A root is an enabled Automation nothing enabled hands to. Ordered the way
                        -- the board receives them (ListAutomations.cs:43 orders by trigger label),
                        -- with the id as a tiebreaker so the walk is deterministic.
                        FOR root IN
                            SELECT a."Id"
                            FROM projects.automations a
                            WHERE a."ProjectId" = project
                              AND a."Enabled"
                              AND NOT EXISTS (
                                  SELECT 1
                                  FROM projects.automations b
                                  WHERE b."ProjectId" = project
                                    AND b."Enabled"
                                    AND b."ToStage" IS NOT NULL
                                    AND lower(b."ToStage") = lower(a."TriggerLabel")
                              )
                            ORDER BY a."TriggerLabel", a."Id"
                        LOOP
                            chain := '{}';
                            node := root;
                            WHILE node IS NOT NULL AND NOT (node = ANY (placed)) LOOP
                                placed := placed || node;
                                SELECT a."TriggerLabel",
                                       (
                                           SELECT onward."Id"
                                           FROM projects.automations onward
                                           WHERE onward."ProjectId" = project
                                             AND onward."Enabled"
                                             AND lower(onward."TriggerLabel") = lower(a."ToStage")
                                           -- Two enabled Automations may share a trigger label with
                                           -- different states, and the board's byTrigger is built
                                           -- with new Map(...), where the last entry wins.
                                           ORDER BY onward."TriggerLabel" DESC, onward."Id" DESC
                                           LIMIT 1
                                       )
                                INTO trigger_label, handed_to
                                FROM projects.automations a
                                WHERE a."Id" = node;

                                chain := chain || trigger_label;
                                node := handed_to;
                            END LOOP;

                            -- A chain of one is an Automation outside the flow (DEC-053). It still
                            -- gets a stage, but after the flow, because that is where the board puts
                            -- it (KanbanBoard.tsx:129-137).
                            IF coalesce(array_length(chain, 1), 0) > 1 THEN
                                flow := flow || chain;
                            ELSE
                                loose := loose || chain;
                            END IF;
                        END LOOP;

                        -- A cycle has no root, so its members are still unplaced. Kept rather than
                        -- dropped, for the reason the board keeps them: a Story can be carrying that
                        -- label right now and has to be somewhere.
                        FOR orphan IN
                            SELECT a."Id", a."TriggerLabel"
                            FROM projects.automations a
                            WHERE a."ProjectId" = project
                              AND a."Enabled"
                              AND NOT (a."Id" = ANY (placed))
                            ORDER BY a."TriggerLabel", a."Id"
                        LOOP
                            placed := placed || orphan."Id";
                            loose := loose || orphan."TriggerLabel";
                        END LOOP;

                        -- One label twice is one stage, folded the way the vendor folds it, and the
                        -- first spelling wins because it is the one that was on screen.
                        stages := '{}';
                        FOREACH stage IN ARRAY (flow || loose) LOOP
                            IF NOT EXISTS (
                                SELECT 1 FROM unnest(stages) AS held(value)
                                WHERE lower(held.value) = lower(stage)
                            ) THEN
                                stages := stages || stage;
                            END IF;
                        END LOOP;

                        UPDATE projects.projects
                        SET "LifecycleStages" = stages::character varying(200)[]
                        WHERE "Id" = project;
                    END LOOP;
                END
                $walk$;
                """
            );

            // Step 5. The guard, and the reason this migration is trusted at all: not "it ran", but
            // that every Automation which handed on claims a transition, no Automation invented one,
            // and every label is still there as either the claim or a mark. It raises inside the
            // migration's transaction, so a deployment that would have lost an edge does not start
            // with a correct schema and a wiped workflow — it refuses to start.
            migrationBuilder.Sql(
                """
                DO $verify$
                DECLARE
                    lost text;
                BEGIN
                    SELECT string_agg(
                               format(
                                   '%s: handed on before=%s, claims now=%s, labels before=%s, marks now=%s',
                                   a."Id", audit.handed_on, a."ToStage" IS NOT NULL,
                                   audit.labels, cardinality(a."OutputLabels")
                               ),
                               '; '
                           )
                    INTO lost
                    FROM handoffs_before audit
                    JOIN projects.automations a ON a."Id" = audit.automation
                    WHERE audit.handed_on <> (a."ToStage" IS NOT NULL)
                       OR audit.labels <> cardinality(a."OutputLabels")
                                           + (CASE WHEN a."ToStage" IS NULL THEN 0 ELSE 1 END);

                    IF lost IS NOT NULL THEN
                        RAISE EXCEPTION
                            'ClaimedTransition would have changed a configured hand-off: %', lost;
                    END IF;
                END
                $verify$;
                """
            );

            migrationBuilder.Sql("""DROP TABLE handoffs_before;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The claim goes back to the front of the label set, because the front is where the old
            // walk reads the edge from. Lossy, and deliberately not disguised: a project's stage
            // *order* has nowhere to go in the old shape — which is ADR-0022's whole subject — and a
            // stage no label mentions disappears with it.
            migrationBuilder.Sql(
                """
                UPDATE projects.automations
                SET "OutputLabels" = (ARRAY["ToStage"] || "OutputLabels")::character varying(200)[]
                WHERE "ToStage" IS NOT NULL;
                """
            );

            migrationBuilder.DropColumn(
                name: "LifecycleStages",
                schema: "projects",
                table: "projects"
            );

            migrationBuilder.DropColumn(name: "ToStage", schema: "projects", table: "automations");
        }
    }
}
