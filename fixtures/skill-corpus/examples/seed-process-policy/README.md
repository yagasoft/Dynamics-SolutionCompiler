# Seed Process Policy

This neutral unmanaged seed is the current reusable foothold for the `Process and Service Policy` family.

It intentionally stays compact:
- one duplicate rule on `account`
- one duplicate-rule condition on `account.name`
- one routing rule
- one routing-rule item with a neutral queue target proven in `Dev`
- one mobile offline profile
- one mobile offline profile item scoped to `account`

Use it for:
- source inventory of unpacked process-policy artifacts
- normalization and drift checks for stored duplicate-rule definitions
- normalization and drift checks for stored routing-rule definitions
- normalization and drift checks for stored mobile-offline profile definitions
- inspecting real readback-bundle FormXML and `formjson` helper output through `readback-form-summary.md` or `readback-form-summary.json`
- understanding how Dataverse can export process-policy artifacts into dedicated unpacked folders instead of relying on `Customizations.xml`

Important limits:
- the current seed proves duplicate-rule, routing-rule, and mobile-offline definition handling, not broader service-policy behavior
- duplicate-rule publish or activation still needs best-effort handling or Maker Portal proof in this environment
- routing-rule workflow linkage and queue targeting are still best-effort in source analysis because the unpacked source does not preserve those live associations cleanly
- mobile-offline item readback exposes richer live flags than the export, so drift compares the stable profile and item overlap rather than every runtime field
- SLAs are not represented in this live neutral seed yet; use the separate `references/examples/source-only-sla/` fixture for source-first SLA and SLA-item reasoning, and use `references/examples/source-only-similarity-rule/` for similarity-rule source-first reasoning
- this seed intentionally uses a neutral standard-table rule because the custom seed table rejected duplicate detection in this environment
