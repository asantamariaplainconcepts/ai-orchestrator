# connector-seam

## ADDED Requirements

### Requirement: the Connector reports a change's file changes with their patches

The seam SHALL expose the files a change touches — path, status, added and removed line counts,
and the vendor's unified patch — in vendor-neutral types. When a patch is unavailable the file
SHALL carry an explicit reason (binary content, or a patch beyond the size bound) rather than an
empty or truncated patch. The documents list (UC-023) SHALL be a projection of this same read,
not a second vendor call.

#### Scenario: the changed files are reported with their diffs

- **WHEN** a change touching text files is read through the seam
- **THEN** each file reports its path, status, counts and unified patch

#### Scenario: a patch that cannot be shown says why

- **WHEN** a file is binary, or its patch exceeds the bound
- **THEN** the file reports the reason and carries no patch — never a truncated one
