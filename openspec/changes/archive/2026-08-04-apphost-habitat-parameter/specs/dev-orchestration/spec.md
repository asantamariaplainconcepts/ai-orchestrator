# dev-orchestration — delta for apphost-habitat-parameter

## ADDED Requirements

### Requirement: run mode takes a habitat parameter, defaulting to the dev loop

The AppHost SHALL read a `habitat` parameter (`Parameters:habitat`, overridable through user
secrets and appsettings) in run mode, defaulting to `local`. `local` SHALL apply the dev loop's
declarations exactly as before this change; `server` SHALL apply the same declaration set the
generated compose carries, so a developer can rehearse the operator's shape under `aspire run`.
An unknown value SHALL refuse at startup naming the valid ones.

The parameter SHALL NOT change publish output: publishing always emits the server declarations,
and the artifact carries no habitat parameter.

#### Scenario: nothing configured is the dev loop

- **WHEN** `aspire run` starts with no habitat configured
- **THEN** every declaration matches today's dev loop, unchanged

#### Scenario: the server shape is rehearsable locally

- **WHEN** `Parameters:habitat` is `server` under `aspire run`
- **THEN** the Server receives the same declarations the generated compose carries — pods, the
  Local-locus reason, no seeder

#### Scenario: an unknown habitat refuses by name

- **WHEN** `Parameters:habitat` is neither `local` nor `server`
- **THEN** startup refuses naming both valid values

#### Scenario: the declaration sets are named blocks

- **WHEN** a change adds a declaration to a shape
- **THEN** it is one edit in one named method, and the publish output is regenerated with no
  unrelated diff
