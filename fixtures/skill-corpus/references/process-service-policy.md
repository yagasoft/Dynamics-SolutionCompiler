# Process And Service Policy

This reference covers Dataverse process and service-policy artifacts: workflows, duplicate rules, routing rules, SLAs, similarity rules, and mobile offline profiles.

## How To Think About This Family

- treat these as governed behavior assets rather than schema assets
- inventory the owning rule, policy, or process first, then inspect dependent tables, queues, and forms only if they are part of the behavior
- do not assume they should be authored or reasoned about like entity metadata

## What To Inventory

- workflows and process definitions
- duplicate rules and duplicate-rule conditions
- routing rules and routing-rule items
- SLA definitions, KPI instances, and related service timelines
- similarity rules and their matching criteria
- mobile offline profiles and the records, tables, or views they include

Current neutral foothold:
- `references/examples/seed-process-policy/` now proves duplicate-rule definition handling with one compact rule and one condition, plus one routing rule, one routing-rule item, one mobile offline profile, and one mobile offline profile item with stable source normalization plus best-effort live readback
- the exported source for that seed materializes under `duplicaterules/<hash>/duplicaterule.xml`, so do not assume process-policy artifacts will always appear inside `Customizations.xml`
- the same seed also exports routing-rule source under `RoutingRules/<name>.meta.xml`, so this family can mix dedicated folders and thin manifest shells in the same solution
- the same seed also exports mobile offline source under `MobileOfflineProfiles/<name>.xml`, with child items nested under the owning profile file
- `references/examples/source-only-similarity-rule/` provides a compact source-only `SimilarityRule` fixture for parser and normalization work when live parity is not available
- `references/examples/source-only-sla/` provides a compact source-only `SLA` and `SLAItem` fixture for parser and normalization work when a neutral live SLA seed is blocked by service-table prerequisites

## How To Normalize And Compare

- normalize by policy family, name, state, scope, and referenced components
- separate rule metadata from any records or entities the rule touches
- compare enabled state, scope, and targeting before comparing deeper criteria
- treat exported XML and readback as complementary views of the same policy family, not as interchangeable authoring surfaces
- for duplicate rules, compare the rule and the condition separately and normalize boolean flags so `1/0` source values line up with `true/false` readback
- for routing rules, compare the rule shell and item condition XML directly, but treat workflow links and queue targets as best-effort if the unpacked source omits them
- for mobile offline profiles, compare the profile shell plus the nested item shell, but treat richer live-only booleans as secondary when the export only preserves the stable profile, item, entity, and ownership/distribution overlap
- for similarity rules, normalize rule identity plus targeting fields such as base entity, matching entity, inactive-record handling, `maxkeywords`, and `ngramsize`, but keep the family best-effort if the Web API surface does not expose normal create or list operations for the table
- for SLAs, normalize the SLA shell separately from its SLA items and compare stable fields such as applicable-from, default flag, pause or resume allowance, applicable entity, and any action-flow unique name before treating deeper service automation details as drift

## Direct Dev Versus Tracked Release

- use direct Dataverse mutation in `Dev` when you need to validate runtime behavior, rule activation, or readback
- use tracked solution source and PAC packaging when the policy must move through governed release pipelines
- use both when you are synthesizing the intended policy behavior and still need durable release artifacts
- use a neutral standard-table rule when the environment refuses duplicate detection on a custom seed table; record that choice as an assumption instead of blocking the skill
- use a neutral queue plus an explicitly recorded workflow assumption when routing-rule proof is possible in `Dev` but the exported source does not preserve those live links
- use a neutral mobile-offline profile on a standard table when the environment exposes a compact profile and nested-item export; record any live-only flags as best-effort instead of forcing false drift
- if similarity-rule metadata exposes writable fields but the practical Web API surface still omits normal create or list operations, treat the family as source-first or record-id-based best-effort rather than inventing unsupported parity
- if SLA creation is rejected because the table is not `IsSLAEnabled`, defer the neutral SLA seed until a service-capable standard table is available instead of forcing a project-specific workaround
- when that live SLA constraint blocks unattended neutral seeding, keep moving with the bundled `references/examples/source-only-sla/` parser fixture and label the family source-first until a live seed becomes practical

## Boundaries

- do not treat these artifacts as schema-first components
- do not infer service behavior solely from exported XML when readback or runtime validation is available
- keep project-specific examples out of global guidance unless the task is explicitly tied to that project
- do not assume classic root-component numbers are the only source of truth; live solutioncomponents may use modern internal ids while the unpacked export still contains the real artifact files
