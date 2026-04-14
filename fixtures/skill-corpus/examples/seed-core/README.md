# Seed Core

`CodexMetadataSeedCore` is the neutral baseline example for reusable Dataverse metadata reasoning.

It includes:
- custom tables rooted under the `cdxmeta` publisher
- representative columns across common data types
- representative local choice, shared global choice, and boolean metadata
- a one-to-many relationship
- solution export and unpacked XML
- normalized source and live readback snapshots

Use this example when you need to:
- inspect `solution.xml` and `customizations.xml`
- reason about entity-root exports
- inspect source versus readback for local and global choice metadata
- compare tracked source against live readback
- identify which artifacts are needed for new schema changes

Current boundary:
- the seed now includes a real global option-set contract at `OptionSets/cdxmeta_priorityband.xml` and a bound `cdxmeta_priorityband` column on `cdxmeta_workitem`
- the seed now includes an external-code business-key shell for future alternate-key work
- unattended alternate-key creation is still treated as best-effort in this environment, so the current shipped proof is strongest for option sets rather than live key rows
