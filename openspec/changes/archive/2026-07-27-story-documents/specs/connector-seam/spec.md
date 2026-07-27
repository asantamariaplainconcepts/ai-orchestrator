# connector-seam

## ADDED Requirements

### Requirement: the Connector can find a Story's linked change and read its documents

The Connector seam SHALL expose, in vendor-neutral vocabulary, the ability to find the change
linked to a Story (number, title, URL, head ref) and to read a document's content at a ref. No
vendor noun SHALL appear in the seam's types — a second vendor implements the same two reads
against its own model (work-item relations rather than issue cross-references). Failures SHALL
reuse the existing closed error set so the API's problem codes stay finite.

#### Scenario: the linked change is found through the seam

- **WHEN** a Story has a change that references it
- **THEN** the Connector reports that change with its head ref, through types carrying no
  vendor-specific name

#### Scenario: a Story with no linked change

- **WHEN** no change references the Story
- **THEN** the Connector reports its absence — distinctly from a vendor failure
