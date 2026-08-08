## MODIFIED Requirements

### Requirement: the code-source surface renders only where the API offers it

The Connector settings SHALL offer a Repository / Local folder choice as a segmented control,
rendered only after a single per-page probe of the code-source surface succeeds; where the API
answers 404 (cloud posture), no code-source UI SHALL exist at all — no disabled control, no
explanatory stub. The path input SHALL be monospaced, validated live against the host with the
specific failing check named, and SHALL render loading, empty, error and success states. Recent
folders SHALL be offered as targets at least 44px tall, each naming the project that used it, and
selecting one SHALL re-run the same live validation typing does. A warning callout SHALL state the
**sandbox** constraint (a LocalFolder project cannot run in a sandbox — the sandbox has its own
kernel and cannot see this machine's disk).

#### Scenario: a cloud deployment shows nothing

- **WHEN** the Settings tab renders on a deployment whose code-source probe answers 404
- **THEN** no code-source UI exists at all

#### Scenario: an invalid path names its failing check

- **WHEN** live validation returns for an invalid path
- **THEN** the specific failing check is named and nothing is saved

#### Scenario: a recent folder is not trusted stale

- **WHEN** a recent folder is selected
- **THEN** the live validation runs exactly as if the path were typed

### Requirement: Run now states the locus choice where a choice exists

Run now SHALL dispatch exactly as today, with no dialog, on a project with no local folder. On a
LocalFolder project it SHALL open a dialog of radio cards (targets at least 48px) stating each
locus's consequences; the **sandbox** card SHALL be disabled carrying its reason; the primary
button SHALL repeat the chosen locus ("Run on this machine" / "Run in a sandbox"). Refusals —
BR-001's conflict, BR-013's rules, and the clean-tree refusal recorded by #210 — SHALL render
inside the dialog, announced aria-live polite, naming the folder where the folder is the reason.

The copy said "Agent pod" until this change, in direct contradiction of DEC-005's locked
vocabulary, which has always read *Agent (never "pod")*. Retiring the substrate is what makes the
word finally removable rather than merely wrong.

#### Scenario: no choice, no dialog

- **WHEN** Run now is pressed on a project with no local folder
- **THEN** it dispatches exactly as today with no dialog

#### Scenario: the dialog states the constraint

- **WHEN** Run now opens on a LocalFolder project
- **THEN** the sandbox card is disabled with its reason and the primary button names the chosen
  locus

#### Scenario: a dirty tree refuses inside the dialog

- **WHEN** a local dispatch is attempted against a dirty working tree
- **THEN** the refusal renders in the dialog before any write, naming the folder

### Requirement: every Run says where it executed

The Run detail SHALL show a locus chip beside the state pill and an Execution block in the rail:
runtime and kind, the working folder for local Runs, the branch created, and the output as a local
branch name or a PR link by locus. The Changes card SHALL name the created branch for local Runs
rather than implying a readable working tree. The projects list SHALL mark LocalFolder projects
with a quiet outline "Local" badge — monitor glyph plus the word, the same chip vocabulary as the
Run locus chip. Locus SHALL never be conveyed by colour alone.

#### Scenario: a local Run names its folder and branch

- **WHEN** a local Run's detail renders
- **THEN** the locus chip reads Local and the Execution block shows the working folder and the
  created branch

#### Scenario: a sandbox Run links its output

- **WHEN** a sandbox Run's detail renders
- **THEN** the locus chip reads "In a sandbox" and the output is the PR link as today
